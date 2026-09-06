using System.Runtime.CompilerServices;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Auth;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Services;

public sealed class AskJobEventStreamUseCase : IAskJobEventStreamUseCase
{
    private enum LiveStepKind { Stop, Heartbeat, Event }

    private readonly record struct LiveStepResult(LiveStepKind Kind, AskJobEventRecord? EventRecord = null);

    private readonly IAskJobEventQueryService _events;
    private readonly IAskJobRepository _jobs;
    private readonly IAskJobEventFanout _fanout;
    private readonly IUserSessionManager _sessions;

    public AskJobEventStreamUseCase(
        IAskJobEventQueryService events,
        IAskJobRepository jobs,
        IAskJobEventFanout fanout,
        IUserSessionManager sessions)
    {
        _events = events;
        _jobs = jobs;
        _fanout = fanout;
        _sessions = sessions;
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
            await foreach (var item in ReplayMissedEventsAsync(jobId, lastEventId, latest, cancellationToken).ConfigureAwait(false))
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
            await foreach (var item in StreamLiveLoopAsync(subscription, sessionId, lastSentEventId, cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }

    private async IAsyncEnumerable<AskJobStreamItem> ReplayMissedEventsAsync(
        Guid jobId,
        long lastEventId,
        AskJobEventRecord? latest,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var replay = await _events.GetSinceAsync(jobId, lastEventId, cancellationToken).ConfigureAwait(false);
        foreach (var eventRecord in replay)
        {
            yield return new AskJobStreamItem(AskJobStreamItemKind.Event, eventRecord, IsReplay: true);
        }
    }

    private async IAsyncEnumerable<AskJobStreamItem> StreamLiveLoopAsync(
        IAskJobEventSubscription subscription,
        string? sessionId,
        long initialLastSentEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var streamLifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        streamLifetimeCts.CancelAfter(TimeSpan.FromMinutes(10));
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(20));

        var lastSentEventId = initialLastSentEventId;
        Task<AskJobEventRecord>? readTask = null;
        var heartbeatTask = heartbeatTimer.WaitForNextTickAsync(streamLifetimeCts.Token).AsTask();

        while (!streamLifetimeCts.Token.IsCancellationRequested)
        {
            readTask ??= subscription.Reader.ReadAsync(streamLifetimeCts.Token).AsTask();
            var step = await ReadNextLiveStepAsync(readTask, heartbeatTask).ConfigureAwait(false);

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
        Task<bool> heartbeatTask)
    {
        try
        {
            var completedTask = await Task.WhenAny(readTask, heartbeatTask).ConfigureAwait(false);

            if (completedTask == heartbeatTask)
            {
                var isTickAvailable = await heartbeatTask.ConfigureAwait(false);
                return isTickAvailable ? new LiveStepResult(LiveStepKind.Heartbeat) : new LiveStepResult(LiveStepKind.Stop);
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
