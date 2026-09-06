using System.Runtime.CompilerServices;
using System.Diagnostics.Metrics;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Auth;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Services;

public sealed class AskJobEventStreamUseCase : IAskJobEventStreamUseCase
{
    private static readonly Meter Metrics = new("LegalAssistant.Ask");
    private static readonly Counter<long> ReconciledEvents = Metrics.CreateCounter<long>(
        "ask.sse.reconciled_events",
        unit: "events",
        description: "Ask events delivered from the durable event log while an SSE stream was live.");
    private static readonly TimeSpan DefaultReconciliationInterval = TimeSpan.FromSeconds(5);

    private enum LiveStepKind { Stop, Heartbeat, Reconcile, Event }

    private readonly record struct LiveStepResult(LiveStepKind Kind, AskJobEventRecord? EventRecord = null);

    private readonly IAskJobEventQueryService _events;
    private readonly IAskJobRepository _jobs;
    private readonly IAskJobEventFanout _fanout;
    private readonly IUserSessionManager _sessions;
    private readonly TimeSpan _reconciliationInterval;

    public AskJobEventStreamUseCase(
        IAskJobEventQueryService events,
        IAskJobRepository jobs,
        IAskJobEventFanout fanout,
        IUserSessionManager sessions,
        TimeSpan? reconciliationInterval = null)
    {
        _events = events;
        _jobs = jobs;
        _fanout = fanout;
        _sessions = sessions;
        _reconciliationInterval = reconciliationInterval ?? DefaultReconciliationInterval;

        if (_reconciliationInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(reconciliationInterval), "The reconciliation interval must be positive.");
    }

    public async IAsyncEnumerable<AskJobStreamItem> StreamEventsAsync(
        Guid jobId,
        Guid ownerUserId,
        string? sessionId,
        long lastEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, ownerUserId, cancellationToken);
        if (job == null)
        {
            yield return new AskJobStreamItem(AskJobStreamItemKind.JobNotFound);
            yield break;
        }

        var latest = await _events.GetLatestAsync(jobId, cancellationToken);
        var subscription = _fanout.Subscribe(jobId);

        await using (subscription.ConfigureAwait(false))
        {
            long lastSentEventId = lastEventId;
            var isTerminal = false;

            // 1. Replay missed DB events
            await foreach (var item in ReplayMissedEventsAsync(jobId, lastEventId, cancellationToken).ConfigureAwait(false))
            {
                if (item.EventRecord != null)
                {
                    lastSentEventId = item.EventRecord.Id;
                }
                yield return item;

                if (item.EventRecord?.Status.IsTerminal() == true)
                {
                    isTerminal = true;
                }
            }

            if (isTerminal)
                yield break;

            if (latest != null && latest.Status.IsTerminal() && latest.Id <= lastEventId)
                yield break;

            // 2. Stream live events and heartbeats
            await foreach (var item in StreamLiveLoopAsync(jobId, subscription, sessionId, lastSentEventId, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }

    private async IAsyncEnumerable<AskJobStreamItem> ReplayMissedEventsAsync(
        Guid jobId,
        long lastEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var replay = await _events.GetSinceAsync(jobId, lastEventId, cancellationToken).ConfigureAwait(false);
        foreach (var eventRecord in replay)
        {
            yield return new AskJobStreamItem(AskJobStreamItemKind.Event, eventRecord, IsReplay: true);
        }
    }

    private async IAsyncEnumerable<AskJobStreamItem> StreamLiveLoopAsync(
        Guid jobId,
        IAskJobEventSubscription subscription,
        string? sessionId,
        long initialLastSentEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var streamLifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        streamLifetimeCts.CancelAfter(TimeSpan.FromMinutes(10));
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        using var reconciliationTimer = new PeriodicTimer(_reconciliationInterval);

        var lastSentEventId = initialLastSentEventId;
        Task<AskJobEventRecord>? readTask = null;
        var heartbeatTask = heartbeatTimer.WaitForNextTickAsync(streamLifetimeCts.Token).AsTask();
        var reconciliationTask = reconciliationTimer.WaitForNextTickAsync(streamLifetimeCts.Token).AsTask();

        while (!streamLifetimeCts.Token.IsCancellationRequested)
        {
            readTask ??= subscription.Reader.ReadAsync(streamLifetimeCts.Token).AsTask();
            var step = await ReadNextLiveStepAsync(readTask, heartbeatTask, reconciliationTask).ConfigureAwait(false);

            if (step.Kind == LiveStepKind.Stop)
                yield break;

            if (step.Kind == LiveStepKind.Heartbeat)
            {
                heartbeatTask = heartbeatTimer.WaitForNextTickAsync(streamLifetimeCts.Token).AsTask();

                if (!string.IsNullOrWhiteSpace(sessionId) && !await _sessions.ExistsAsync(sessionId, streamLifetimeCts.Token).ConfigureAwait(false))
                {
                    yield return new AskJobStreamItem(AskJobStreamItemKind.SessionExpired);
                    yield break;
                }

                yield return new AskJobStreamItem(AskJobStreamItemKind.Heartbeat);
                continue;
            }

            if (step.Kind == LiveStepKind.Reconcile)
            {
                reconciliationTask = reconciliationTimer.WaitForNextTickAsync(streamLifetimeCts.Token).AsTask();

                var missedEvents = await _events
                    .GetSinceAsync(jobId, lastSentEventId, streamLifetimeCts.Token)
                    .ConfigureAwait(false);

                foreach (var missedEvent in missedEvents)
                {
                    if (missedEvent.Id > 0 && missedEvent.Id <= lastSentEventId)
                        continue;

                    lastSentEventId = missedEvent.Id;
                    ReconciledEvents.Add(1);
                    yield return new AskJobStreamItem(AskJobStreamItemKind.Event, missedEvent, IsReplay: true);

                    if (missedEvent.Status.IsTerminal())
                        yield break;
                }

                continue;
            }

            if (step.Kind == LiveStepKind.Event && step.EventRecord != null)
            {
                readTask = null;

                if (step.EventRecord.Id > 0 && step.EventRecord.Id <= lastSentEventId)
                    continue;

                lastSentEventId = step.EventRecord.Id;
                yield return new AskJobStreamItem(AskJobStreamItemKind.Event, step.EventRecord, IsReplay: false);

                if (step.EventRecord.Status.IsTerminal())
                    yield break;
            }
        }
    }

    private static async Task<LiveStepResult> ReadNextLiveStepAsync(
        Task<AskJobEventRecord> readTask,
        Task<bool> heartbeatTask,
        Task<bool> reconciliationTask)
    {
        try
        {
            var completedTask = await Task.WhenAny(readTask, heartbeatTask, reconciliationTask).ConfigureAwait(false);

            if (completedTask == heartbeatTask)
            {
                var isTickAvailable = await heartbeatTask.ConfigureAwait(false);
                return isTickAvailable ? new LiveStepResult(LiveStepKind.Heartbeat) : new LiveStepResult(LiveStepKind.Stop);
            }

            if (completedTask == reconciliationTask)
            {
                var isTickAvailable = await reconciliationTask.ConfigureAwait(false);
                return isTickAvailable ? new LiveStepResult(LiveStepKind.Reconcile) : new LiveStepResult(LiveStepKind.Stop);
            }

            var eventRecord = await readTask.ConfigureAwait(false);
            return new LiveStepResult(LiveStepKind.Event, eventRecord);
        }
        catch (OperationCanceledException)
        {
            return new LiveStepResult(LiveStepKind.Stop);
        }
    }
}
