using LegalAssistant.Application.Common;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(10);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly IClock _clock;
    private readonly IOutboxNotificationListener _notificationListener;
    private readonly OutboxDispatcherMetrics _metrics;
    private readonly OutboxDispatcherOptions _options;

    public OutboxDispatcherHostedService(
        IServiceProvider serviceProvider,
        ILogger<OutboxDispatcherHostedService> logger,
        IClock clock,
        IOutboxNotificationListener notificationListener,
        OutboxDispatcherMetrics metrics,
        OutboxDispatcherOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _clock = clock;
        _notificationListener = notificationListener;
        _metrics = metrics;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var validationScope = _serviceProvider.CreateScope())
        {
            var publishers = validationScope.ServiceProvider
                .GetServices<IOutboxMessagePublisher>()
                .ToArray();
            ValidatePublisherOwnership(publishers, _options);
        }

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
                foreach (var maintenance in scope.ServiceProvider.GetServices<IOutboxMaintenance>())
                    await maintenance.ExecuteAsync(db, stoppingToken);

                dispatched = await DispatchBatchAsync(db, publishers, _options, stoppingToken) > 0;
                await RefreshMetricsAsync(db, _options, stoppingToken);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Outbox dispatcher stopped during host shutdown.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher cycle failed. owners={Owners} messageTypes={MessageTypes}", string.Join(',', _options.Owners), string.Join(',', _options.MessageTypes));
            }

            if (dispatched)
                continue;

            try
            {
                if (await _notificationListener.WaitAsync(_options.PollInterval, stoppingToken))
                    _metrics.RecordNotificationWakeup();
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Outbox dispatcher wait cancelled during host shutdown.");
                return;
            }
        }
    }

    private async Task<int> DispatchBatchAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<IOutboxMessagePublisher> publishers,
        OutboxDispatcherOptions options,
        CancellationToken cancellationToken)
    {
        if (options.MessageTypes.Count == 0)
            return 0;

        var publisherByType = publishers
            .SelectMany(publisher => publisher.MessageTypes.Select(type => (type, publisher)))
            .ToDictionary(x => x.type, x => x.publisher, StringComparer.Ordinal);
        var messages = await ClaimDueBatchAsync(
            db,
            options.Owners,
            options.MessageTypes,
            options.BatchSize,
            options.LeaseDuration,
            _clock.UtcNow,
            cancellationToken);
        _metrics.RecordClaimed(messages.Count);

        foreach (var message in messages)
        {
            try
            {
                if (!await RenewProcessingLeaseAsync(db, message.Id, message.LeaseId, _clock.UtcNow, options.LeaseDuration, cancellationToken))
                {
                    _logger.LogDebug("Outbox lease was reclaimed before publication; message skipped. outboxMessageId={OutboxMessageId} owner={Owner} leaseId={LeaseId} correlationId={CorrelationId}", message.Id, message.Owner, message.LeaseId, message.CorrelationId);
                    continue;
                }

                if (!publisherByType.TryGetValue(message.MessageType, out var publisher))
                    throw new InvalidOperationException($"No outbox publisher is registered for '{message.MessageType}'.");

                await publisher.PublishAsync(message, cancellationToken);
                if (!await MarkPublishedAsync(db, message.Id, message.LeaseId, _clock.UtcNow, options.LeaseDuration, cancellationToken))
                {
                    _logger.LogDebug("Outbox lease was reclaimed before marking publication. outboxId={OutboxId}", message.Id);
                    continue;
                }
                _metrics.RecordPublished(_clock.UtcNow - message.CreatedAt);
                _logger.LogInformation(
                    "Published outbox message. messageType={MessageType} messageId={MessageId} outboxId={OutboxId}",
                    message.MessageType,
                    message.DeduplicationKey ?? message.Id.ToString("N"),
                    message.Id);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex,
                    "Outbox publication cancelled; leaving the processing lease for expiry recovery. outboxMessageId={OutboxMessageId} owner={Owner} leaseId={LeaseId} correlationId={CorrelationId}",
                    message.Id, message.Owner, message.LeaseId, message.CorrelationId);
                throw;
            }
            catch (Exception ex)
            {
                var attempt = message.Attempts + 1;
                var terminal = attempt >= options.MaxAttempts;
                bool updated;
                try
                {
                    updated = await MarkRetryAsync(
                        db,
                        message.Id,
                        message.LeaseId,
                        ex.Message,
                        _clock.UtcNow.Add(CalculateBackoff(attempt)),
                        terminal,
                        _clock.UtcNow,
                        options.LeaseDuration,
                        cancellationToken);
                }
                catch (Exception persistenceException)
                {
                    _logger.LogError(persistenceException,
                        "Could not persist outbox retry after publish failure. outboxMessageId={OutboxMessageId} owner={Owner} leaseId={LeaseId} correlationId={CorrelationId} attempt={Attempt} publishException={PublishException}",
                        message.Id, message.Owner, message.LeaseId, message.CorrelationId, attempt, ex);
                    throw;
                }
                if (!updated)
                {
                    _logger.LogDebug("Outbox retry update skipped because lease ownership changed. outboxMessageId={OutboxMessageId} owner={Owner} leaseId={LeaseId} attempt={Attempt}", message.Id, message.Owner, message.LeaseId, attempt);
                    continue;
                }
                _metrics.RecordFailed(terminal);
                _logger.LogWarning(
                    ex,
                    "Outbox publish failed; retry state persisted. messageType={MessageType} outboxMessageId={OutboxMessageId} owner={Owner} leaseId={LeaseId} correlationId={CorrelationId} attempt={Attempt} terminal={Terminal}",
                    message.MessageType,
                    message.Id,
                    message.Owner,
                    message.LeaseId,
                    message.CorrelationId,
                    attempt,
                    terminal);
            }
        }

        return messages.Count;
    }

    private async Task<IReadOnlyList<OutboxMessageRecord>> ClaimDueBatchAsync(
        LegalAssistantDbContext db,
        IReadOnlyCollection<string> owners,
        IReadOnlyCollection<string> messageTypes,
        int batchSize,
        TimeSpan leaseDuration,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var staleBefore = utcNow - leaseDuration;
        var candidates = await db.OutboxMessages
            .AsNoTracking()
            .Where(x => owners.Contains(x.Owner))
            .Where(x => messageTypes.Contains(x.MessageType))
            .Where(x =>
                (x.Status == OutboxMessageStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore))
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var claimed = new List<OutboxMessageRecord>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var leaseId = Guid.NewGuid();
            int updated;
            if (db.Database.IsRelational())
            {
                updated = await db.OutboxMessages
                    .Where(x => x.Id == candidate.Id &&
                                x.Owner == candidate.Owner &&
                                x.MessageType == candidate.MessageType &&
                                x.Version == candidate.Version &&
                                ((x.Status == OutboxMessageStatus.Pending &&
                                  (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                                 (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore)))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, OutboxMessageStatus.Processing)
                        .SetProperty(x => x.LeaseId, leaseId)
                        .SetProperty(x => x.UpdatedAt, utcNow)
                        .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
            }
            else
            {
                var tracked = await db.OutboxMessages.FirstOrDefaultAsync(x =>
                    x.Id == candidate.Id &&
                    x.Owner == candidate.Owner &&
                    x.MessageType == candidate.MessageType &&
                    x.Version == candidate.Version &&
                    ((x.Status == OutboxMessageStatus.Pending &&
                      (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                     (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore)), cancellationToken);
                if (tracked is null)
                    continue;

                tracked.Status = OutboxMessageStatus.Processing;
                tracked.LeaseId = leaseId;
                tracked.UpdatedAt = utcNow;
                tracked.Version++;
                await db.SaveChangesAsync(cancellationToken);
                updated = 1;
            }

            if (updated != 1)
            {
                _logger.LogDebug("Outbox claim lost to another dispatcher; candidate skipped. outboxMessageId={OutboxMessageId} owner={Owner} messageType={MessageType} observedVersion={Version}", candidate.Id, candidate.Owner, candidate.MessageType, candidate.Version);
                continue;
            }

            candidate.Status = OutboxMessageStatus.Processing;
            candidate.LeaseId = leaseId;
            candidate.UpdatedAt = utcNow;
            candidate.Version++;
            claimed.Add(candidate);
        }

        return claimed;
    }

    private async Task RefreshMetricsAsync(
        LegalAssistantDbContext db,
        OutboxDispatcherOptions options,
        CancellationToken cancellationToken)
    {
        if (options.MessageTypes.Count == 0)
            return;

        var staleBefore = _clock.UtcNow - options.LeaseDuration;
        var snapshot = await db.OutboxMessages
            .AsNoTracking()
            .Where(x => options.Owners.Contains(x.Owner))
            .Where(x => options.MessageTypes.Contains(x.MessageType))
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

    private static void ValidatePublisherOwnership(
        IReadOnlyCollection<IOutboxMessagePublisher> publishers,
        OutboxDispatcherOptions options)
    {
        var registrations = publishers
            .SelectMany(publisher => publisher.MessageTypes.Select(messageType => (publisher, messageType)))
            .ToArray();

        foreach (var registration in registrations)
        {
            var expectedOwner = OutboxMessageOwners.ForMessageType(registration.messageType);
            if (!options.Owners.Contains(expectedOwner))
                throw new InvalidOperationException(
                    $"Dispatcher for '{string.Join(",", options.Owners)}' registered message type '{registration.messageType}' owned by '{expectedOwner}'.");

            if (!string.Equals(registration.publisher.Owner, expectedOwner, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Publisher '{registration.publisher.GetType().Name}' declares owner '{registration.publisher.Owner}' for '{registration.messageType}', expected '{expectedOwner}'.");
        }

        var duplicateTypes = registrations
            .GroupBy(x => x.messageType, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateTypes.Length > 0)
            throw new InvalidOperationException(
                $"Multiple outbox publishers are registered for: {string.Join(", ", duplicateTypes)}.");

        var missingTypes = options.MessageTypes
            .Except(registrations.Select(x => x.messageType), StringComparer.Ordinal)
            .ToArray();
        if (missingTypes.Length > 0)
            throw new InvalidOperationException(
                $"No outbox publisher is registered for: {string.Join(", ", missingTypes)}.");
    }

    private static async Task<bool> MarkPublishedAsync(
        LegalAssistantDbContext db,
        Guid id,
        Guid? leaseId,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
        {
            var updated = await db.OutboxMessages
                .Where(x => x.Id == id && x.Status == OutboxMessageStatus.Processing && x.LeaseId == leaseId && x.UpdatedAt > utcNow.Subtract(leaseDuration))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, OutboxMessageStatus.Published)
                    .SetProperty(x => x.PublishedAt, utcNow)
                    .SetProperty(x => x.UpdatedAt, utcNow)
                    .SetProperty(x => x.LeaseId, (Guid?)null)
                    .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
            return updated > 0;
        }

        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null || message.Status != OutboxMessageStatus.Processing || message.LeaseId != leaseId || message.UpdatedAt <= utcNow.Subtract(leaseDuration))
            return false;

        message.Status = OutboxMessageStatus.Published;
        message.PublishedAt = utcNow;
        message.UpdatedAt = utcNow;
        message.LeaseId = null;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> MarkRetryAsync(
        LegalAssistantDbContext db,
        Guid id,
        Guid? leaseId,
        string error,
        DateTime nextAttemptAt,
        bool terminal,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
        {
            var updated = await db.OutboxMessages
                .Where(x => x.Id == id && x.Status == OutboxMessageStatus.Processing && x.LeaseId == leaseId && x.UpdatedAt > utcNow.Subtract(leaseDuration))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.LastError, error)
                    .SetProperty(x => x.UpdatedAt, utcNow)
                    .SetProperty(x => x.NextAttemptAt, nextAttemptAt)
                    .SetProperty(x => x.Status, terminal ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending)
                    .SetProperty(x => x.LeaseId, (Guid?)null)
                    .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
            return updated > 0;
        }

        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null || message.Status != OutboxMessageStatus.Processing || message.LeaseId != leaseId || message.UpdatedAt <= utcNow.Subtract(leaseDuration))
            return false;

        message.Attempts += 1;
        message.LastError = error;
        message.UpdatedAt = utcNow;
        message.NextAttemptAt = nextAttemptAt;
        message.Status = terminal ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
        message.LeaseId = null;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> RenewProcessingLeaseAsync(
        LegalAssistantDbContext db,
        Guid id,
        Guid? leaseId,
        DateTime utcNow,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (leaseId is null)
            return false;

        if (db.Database.IsRelational())
        {
            var updated = await db.OutboxMessages
                .Where(x => x.Id == id && x.Status == OutboxMessageStatus.Processing && x.LeaseId == leaseId && x.UpdatedAt > utcNow.Subtract(leaseDuration))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.UpdatedAt, utcNow), cancellationToken);
            return updated > 0;
        }

        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message is null || message.Status != OutboxMessageStatus.Processing || message.LeaseId != leaseId || message.UpdatedAt <= utcNow.Subtract(leaseDuration))
            return false;

        message.UpdatedAt = utcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TimeSpan CalculateBackoff(int attempt)
    {
        var multiplier = Math.Min(8, Math.Max(0, attempt - 1));
        return TimeSpan.FromSeconds(RetryBaseDelay.TotalSeconds * Math.Pow(2, multiplier));
    }

    private static bool IsPostgres(LegalAssistantDbContext db)
        => db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}
