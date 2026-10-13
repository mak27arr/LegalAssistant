using System.Diagnostics.Metrics;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcherMetrics
{
    private static readonly Meter Meter = new("LegalAssistant.Outbox");

    private long _pendingRows;
    private long _failedRows;
    private long _staleProcessingRows;
    private double _oldestPendingAgeSeconds;

    public OutboxDispatcherMetrics()
    {
        Meter.CreateObservableGauge("outbox.pending.rows", () => Volatile.Read(ref _pendingRows));
        Meter.CreateObservableGauge("outbox.failed.rows", () => Volatile.Read(ref _failedRows));
        Meter.CreateObservableGauge("outbox.stale_processing.rows", () => Volatile.Read(ref _staleProcessingRows));
        Meter.CreateObservableGauge("outbox.oldest_pending.age_seconds", () => Volatile.Read(ref _oldestPendingAgeSeconds));

        Claimed = Meter.CreateCounter<long>("outbox.messages.claimed");
        Published = Meter.CreateCounter<long>("outbox.messages.published");
        Failed = Meter.CreateCounter<long>("outbox.messages.failed");
        TerminalFailures = Meter.CreateCounter<long>("outbox.messages.terminal_failures");
        NotificationWakeups = Meter.CreateCounter<long>("outbox.notification.wakeups");
        DispatchLatency = Meter.CreateHistogram<double>("outbox.dispatch.latency", "ms");
    }

    public Counter<long> Claimed { get; }
    public Counter<long> Published { get; }
    public Counter<long> Failed { get; }
    public Counter<long> TerminalFailures { get; }
    public Counter<long> NotificationWakeups { get; }
    public Histogram<double> DispatchLatency { get; }

    public void RecordClaimed(int count) => Claimed.Add(count);

    public void RecordPublished(TimeSpan latency)
    {
        Published.Add(1);
        DispatchLatency.Record(Math.Max(0, latency.TotalMilliseconds));
    }

    public void RecordFailed(bool terminal)
    {
        Failed.Add(1);
        if (terminal)
            TerminalFailures.Add(1);
    }

    public void RecordNotificationWakeup() => NotificationWakeups.Add(1);

    public void RecordSnapshot(
        int pendingRows,
        DateTime? oldestPendingAt,
        int failedRows,
        int staleProcessingRows,
        DateTime utcNow)
    {
        Volatile.Write(ref _pendingRows, pendingRows);
        Volatile.Write(ref _failedRows, failedRows);
        Volatile.Write(ref _staleProcessingRows, staleProcessingRows);
        Volatile.Write(
            ref _oldestPendingAgeSeconds,
            oldestPendingAt is null ? 0 : Math.Max(0, (utcNow - oldestPendingAt.Value).TotalSeconds));
    }

}
