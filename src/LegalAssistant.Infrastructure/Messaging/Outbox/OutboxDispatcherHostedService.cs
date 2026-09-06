using LegalAssistant.Application.Common;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 25;
    private const int MaxAttempts = 10;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly IClock _clock;
    private readonly IOutboxNotificationListener _notificationListener;
    private readonly OutboxDispatcherMetrics _metrics;

    public OutboxDispatcherHostedService(
        IServiceProvider serviceProvider,
        ILogger<OutboxDispatcherHostedService> logger,
        IClock clock,
        IOutboxNotificationListener notificationListener,
        OutboxDispatcherMetrics metrics)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _clock = clock;
        _notificationListener = notificationListener;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _notificationListener.Start(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var dispatched = false;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                var publishers = scope.ServiceProvider
                    .GetServices<IOutboxMessagePublisher>()
                    .ToArray();
                var messageTypes = publishers
                    .SelectMany(x => x.MessageTypes)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                foreach (var maintenance in scope.ServiceProvider.GetServices<IOutboxMaintenance>())
                    await maintenance.ExecuteAsync(db, stoppingToken);

                dispatched = await DispatchBatchAsync(db, publishers, messageTypes, stoppingToken) > 0;
                await RefreshMetricsAsync(db, messageTypes, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher cycle failed");
            }

            if (dispatched)
                continue;

            try
            {
                if (await _notificationListener.WaitAsync(PollDelay, stoppingToken))
                    _metrics.RecordNotificationWakeup();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<int> DispatchBatchAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<IOutboxMessagePublisher> publishers,
        IReadOnlyCollection<string> messageTypes,
        CancellationToken cancellationToken)
    {
        if (messageTypes.Count == 0)
            return 0;

        var publisherByType = publishers
            .SelectMany(publisher => publisher.MessageTypes.Select(type => (type, publisher)))
            .ToDictionary(x => x.type, x => x.publisher, StringComparer.Ordinal);
        var messages = await ClaimDueBatchAsync(db, messageTypes, _clock.UtcNow, cancellationToken);
        _metrics.RecordClaimed(messages.Count);

        foreach (var message in messages)
        {
            try
            {
                if (!publisherByType.TryGetValue(message.MessageType, out var publisher))
                    throw new InvalidOperationException($"No outbox publisher is registered for '{message.MessageType}'.");

                await publisher.PublishAsync(message, cancellationToken);
                await MarkPublishedAsync(db, message.Id, _clock.UtcNow, cancellationToken);
                _metrics.RecordPublished(_clock.UtcNow - message.CreatedAt);
                _logger.LogInformation(
                    "Published outbox message. messageType={MessageType} messageId={MessageId} outboxId={OutboxId}",
                    message.MessageType,
                    message.DeduplicationKey ?? message.Id.ToString("N"),
                    message.Id);
            }
            catch (Exception ex)
            {
                var attempt = message.Attempts + 1;
                var terminal = attempt >= MaxAttempts;
                await MarkRetryAsync(
                    db,
                    message.Id,
                    ex.Message,
                    _clock.UtcNow.Add(CalculateBackoff(attempt)),
                    terminal,
                    _clock.UtcNow,
                    cancellationToken);
                _metrics.RecordFailed(terminal);
                _logger.LogWarning(
                    ex,
                    "Failed to publish outbox message. messageType={MessageType} outboxId={OutboxId} attempt={Attempt}",
                    message.MessageType,
                    message.Id,
                    attempt);
            }
        }

        return messages.Count;
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> ClaimDueBatchAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<string> messageTypes,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var staleBefore = utcNow - StaleLease;

        return await ClaimWithEfTransactionAsync(db, messageTypes, utcNow, staleBefore, cancellationToken);
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> ClaimWithEfTransactionAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<string> messageTypes,
        DateTime utcNow,
        DateTime staleBefore,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return await ClaimNonRelationalBatchAsync(db, messageTypes, utcNow, staleBefore, cancellationToken);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages
            .Where(x => messageTypes.Contains(x.MessageType))
            .Where(x =>
                (x.Status == OutboxMessageStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore))
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        return await MarkBatchProcessingAsync(db, tx, messages, utcNow, cancellationToken);
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> ClaimNonRelationalBatchAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<string> messageTypes,
        DateTime utcNow,
        DateTime staleBefore,
        CancellationToken cancellationToken)
    {
        var messages = await db.OutboxMessages
            .Where(x => messageTypes.Contains(x.MessageType))
            .Where(x =>
                (x.Status == OutboxMessageStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore))
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        // Use the same concurrency-aware update path as relational providers.
        // A conflict means another dispatcher won the claim, so this dispatcher
        // must abandon the whole batch and publish nothing from it.
        return await MarkBatchProcessingAsync(db, null, messages, utcNow, cancellationToken);
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> MarkBatchProcessingAsync(
        LegalAssistantDbContext db,
        IDbContextTransaction? tx,
        IReadOnlyList<OutboxMessageRecord> messages,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            if (tx is not null)
                await tx.CommitAsync(cancellationToken);
            return messages;
        }

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.UpdatedAt = utcNow;
            message.Version += 1;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (tx is not null)
                await tx.CommitAsync(cancellationToken);
            return messages;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (tx is not null)
                await tx.RollbackAsync(cancellationToken);
            return [];
        }
    }

    private async Task RefreshMetricsAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<string> messageTypes,
        CancellationToken cancellationToken)
    {
        if (messageTypes.Count == 0)
            return;

        var staleBefore = _clock.UtcNow - StaleLease;
        var snapshot = await db.OutboxMessages
            .AsNoTracking()
            .Where(x => messageTypes.Contains(x.MessageType))
            .GroupBy(_ => 1)
            .Select(group => new OutboxSnapshot(
                group.Count(x => x.Status == OutboxMessageStatus.Pending),
                group.Where(x => x.Status == OutboxMessageStatus.Pending)
                    .Select(x => (DateTime?)x.CreatedAt)
                    .Min(),
                group.Count(x => x.Status == OutboxMessageStatus.Failed),
                group.Count(x => x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore)))
            .SingleOrDefaultAsync(cancellationToken);

        _metrics.RecordSnapshot(
            snapshot.PendingRows,
            snapshot.OldestPendingAt,
            snapshot.FailedRows,
            snapshot.StaleProcessingRows,
            _clock.UtcNow);
        if (snapshot.FailedRows > 0 || snapshot.StaleProcessingRows > 0)
        {
            _logger.LogWarning(
                "Outbox health warning. pending={PendingRows} oldestPendingAgeSeconds={OldestPendingAgeSeconds} failed={FailedRows} staleProcessing={StaleProcessingRows}",
                snapshot.PendingRows,
                snapshot.OldestPendingAt is null ? 0 : Math.Max(0, (_clock.UtcNow - snapshot.OldestPendingAt.Value).TotalSeconds),
                snapshot.FailedRows,
                snapshot.StaleProcessingRows);
        }
    }

    private readonly record struct OutboxSnapshot(
        int PendingRows,
        DateTime? OldestPendingAt,
        int FailedRows,
        int StaleProcessingRows);

    private static async Task MarkPublishedAsync(
        LegalAssistantDbContext db,
        Guid id,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null)
            return;

        message.Status = OutboxMessageStatus.Published;
        message.PublishedAt = utcNow;
        message.UpdatedAt = utcNow;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkRetryAsync(
        LegalAssistantDbContext db,
        Guid id,
        string error,
        DateTime nextAttemptAt,
        bool terminal,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null)
            return;

        message.Attempts += 1;
        message.LastError = error;
        message.UpdatedAt = utcNow;
        message.NextAttemptAt = nextAttemptAt;
        message.Status = terminal ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan CalculateBackoff(int attempt)
    {
        var multiplier = Math.Min(8, Math.Max(0, attempt - 1));
        return TimeSpan.FromSeconds(RetryBaseDelay.TotalSeconds * Math.Pow(2, multiplier));
    }
}
