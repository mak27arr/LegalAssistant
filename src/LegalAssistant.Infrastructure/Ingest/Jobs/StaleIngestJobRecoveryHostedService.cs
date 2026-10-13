using LegalAssistant.Application.Common;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LegalAssistant.Infrastructure.Jobs;

public sealed class StaleIngestJobRecoveryHostedService : BackgroundService
{
    private const string LeaseExpiredMessage = "Ingest job lease expired before completion.";
    private const string DeduplicationIndex = "IX_message_outbox_message_type_deduplication_key";

    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly IngestJobProcessingOptions _options;
    private readonly ILogger<StaleIngestJobRecoveryHostedService> _logger;

    public StaleIngestJobRecoveryHostedService(
        IServiceProvider serviceProvider,
        IClock clock,
        IngestJobProcessingOptions options,
        ILogger<StaleIngestJobRecoveryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                await RecoverBatchAsync(db, stoppingToken);
                await RepairDueQueuedJobsAsync(db, stoppingToken);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Ingest recovery is stopping.");
                return;
            }
            catch (RecoveryOperationException ex)
            {
                _logger.LogError(ex, "Ingest recovery operation failed. operation={Operation} jobId={JobId} correlationId={CorrelationId} attempt={Attempt}",
                    ex.Operation, ex.JobId, ex.CorrelationId, ex.Attempt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ingest recovery cycle failed. operation=load-or-scan");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.RecoveryIntervalSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Ingest recovery delay was cancelled during shutdown.");
                return;
            }
        }
    }

    private async Task RecoverBatchAsync(LegalAssistantDbContext db, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var jobs = await db.Jobs.AsNoTracking()
            .Where(j => j.Type == "ingest" &&
                        j.Status == JobStatus.InProgress &&
                        (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
            .OrderBy(j => j.LeaseExpiresAt)
            .ThenBy(j => j.UpdatedAt)
            .ThenBy(j => j.Id)
            .Take(Math.Clamp(_options.RecoveryBatchSize, 1, 200))
            .ToListAsync(cancellationToken);

        foreach (var candidate in jobs)
            await RecoverExpiredJobAsync(db, candidate, now, cancellationToken);
    }

    private async Task RecoverExpiredJobAsync(
        LegalAssistantDbContext db,
        JobRecord candidate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        var terminal = candidate.AttemptCount >= maxAttempts;
        var error = candidate.LastError ?? LeaseExpiredMessage;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            IDbContextTransaction? transaction = null;
            OutboxMessageRecord? insertedOutbox = null;
            try
            {
                if (db.Database.IsRelational())
                    transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                var updated = await TransitionExpiredJobAsync(db, candidate, terminal, error, now, cancellationToken);
                if (updated == 0)
                {
                    await RollbackAsync(transaction, candidate, "expired-job-claim-lost", attempt, null);
                    _logger.LogDebug("Expired ingest recovery lost its conditional update; candidate skipped. jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId} attempt={Attempt}",
                        candidate.Id, candidate.CorrelationId, candidate.LeaseId, attempt);
                    return;
                }

                if (!terminal)
                {
                    var outboxReady = await ResetOrCreateOutboxAsync(db, candidate, now, cancellationToken,
                        value => insertedOutbox = value);
                    if (!outboxReady)
                    {
                        await RollbackAsync(transaction, candidate, "active-outbox-claim", attempt, null);
                        _logger.LogDebug("Expired ingest recovery left the job unchanged because its outbox row has an active dispatcher lease. jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId} attempt={Attempt}",
                            candidate.Id, candidate.CorrelationId, candidate.LeaseId, attempt);
                        return;
                    }
                }

                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);

                if (terminal)
                {
                    _logger.LogError("Stale ingest job exceeded the retry limit and was failed. jobId={JobId} correlationId={CorrelationId} attempt={Attempt}",
                        candidate.Id, candidate.CorrelationId, candidate.AttemptCount);
                }
                else
                {
                    _logger.LogWarning("Requeued stale ingest job and reset its outbox atomically. jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId} attempt={Attempt}",
                        candidate.Id, candidate.CorrelationId, candidate.LeaseId, candidate.AttemptCount);
                }
                return;
            }
            catch (DbUpdateException ex) when (IsDeduplicationConflict(ex))
            {
                if (!await RollbackAsync(transaction, candidate, "outbox-deduplication-conflict", attempt, ex))
                    throw;
                if (insertedOutbox is not null)
                    db.Entry(insertedOutbox).State = EntityState.Detached;

                _logger.LogInformation(ex,
                    "Concurrent recovery inserted the ingest outbox deduplication key; re-reading and retrying the transaction. jobId={JobId} correlationId={CorrelationId} owner={Owner} attempt={Attempt}",
                    candidate.Id, candidate.CorrelationId, OutboxMessageOwners.Ingest, attempt);
                if (attempt == 2)
                    throw new RecoveryOperationException("recover-expired-job", candidate, attempt, ex);

                var fresh = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == candidate.Id, cancellationToken);
                if (fresh is null || fresh.Status != JobStatus.InProgress || fresh.LeaseId != candidate.LeaseId ||
                    (fresh.LeaseExpiresAt is not null && fresh.LeaseExpiresAt > now))
                    return;
            }
            catch (Exception ex)
            {
                await RollbackAsync(transaction, candidate, "recover-expired-job", attempt, ex);
                if (insertedOutbox is not null)
                    db.Entry(insertedOutbox).State = EntityState.Detached;
                throw new RecoveryOperationException("recover-expired-job", candidate, attempt, ex);
            }
            finally
            {
                if (transaction is not null)
                    await transaction.DisposeAsync();
            }
        }
    }

    private async Task RepairDueQueuedJobsAsync(LegalAssistantDbContext db, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var jobs = await db.Jobs.AsNoTracking()
            .Where(j => j.Type == "ingest" &&
                        j.Status == JobStatus.Queued &&
                        j.NextAttemptAt != null &&
                        j.NextAttemptAt <= now)
            .OrderBy(j => j.NextAttemptAt)
            .ThenBy(j => j.Id)
            .Take(Math.Clamp(_options.RecoveryBatchSize, 1, 200))
            .ToListAsync(cancellationToken);

        foreach (var candidate in jobs)
            await ScheduleDueRetryAsync(db, candidate, now, cancellationToken);
    }

    private async Task ScheduleDueRetryAsync(
        LegalAssistantDbContext db,
        JobRecord candidate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            IDbContextTransaction? transaction = null;
            OutboxMessageRecord? insertedOutbox = null;
            try
            {
                if (db.Database.IsRelational())
                    transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                var updated = await ClearDueRetryMarkerAsync(db, candidate, now, cancellationToken);
                if (updated == 0)
                {
                    await RollbackAsync(transaction, candidate, "due-retry-claim-lost", attempt, null);
                    _logger.LogDebug("Queued ingest retry changed before scheduling; candidate skipped. jobId={JobId} correlationId={CorrelationId} observedNextAttemptAt={NextAttemptAt} attempt={Attempt}",
                        candidate.Id, candidate.CorrelationId, candidate.NextAttemptAt, attempt);
                    return;
                }

                var outboxReady = await ResetOrCreateOutboxAsync(db, candidate, now, cancellationToken,
                    value => insertedOutbox = value);
                if (!outboxReady)
                {
                    await RollbackAsync(transaction, candidate, "active-outbox-claim", attempt, null);
                    _logger.LogDebug("Queued ingest retry was not cleared because its outbox row has an active dispatcher lease. jobId={JobId} correlationId={CorrelationId} attempt={Attempt}",
                        candidate.Id, candidate.CorrelationId, attempt);
                    return;
                }

                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Scheduled due ingest retry by atomically resetting its outbox row. jobId={JobId} correlationId={CorrelationId} attempt={Attempt}",
                    candidate.Id, candidate.CorrelationId, attempt);
                return;
            }
            catch (DbUpdateException ex) when (IsDeduplicationConflict(ex))
            {
                if (!await RollbackAsync(transaction, candidate, "outbox-deduplication-conflict", attempt, ex))
                    throw;
                if (insertedOutbox is not null)
                    db.Entry(insertedOutbox).State = EntityState.Detached;
                _logger.LogInformation(ex,
                    "Concurrent retry repair inserted the ingest outbox deduplication key; re-reading and retrying the transaction. jobId={JobId} correlationId={CorrelationId} owner={Owner} attempt={Attempt}",
                    candidate.Id, candidate.CorrelationId, OutboxMessageOwners.Ingest, attempt);
                if (attempt == 2)
                    throw new RecoveryOperationException("schedule-due-retry", candidate, attempt, ex);

                var fresh = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == candidate.Id, cancellationToken);
                if (fresh is null || fresh.Status != JobStatus.Queued || fresh.NextAttemptAt != candidate.NextAttemptAt)
                    return;
            }
            catch (Exception ex)
            {
                await RollbackAsync(transaction, candidate, "schedule-due-retry", attempt, ex);
                if (insertedOutbox is not null)
                    db.Entry(insertedOutbox).State = EntityState.Detached;
                throw new RecoveryOperationException("schedule-due-retry", candidate, attempt, ex);
            }
            finally
            {
                if (transaction is not null)
                    await transaction.DisposeAsync();
            }
        }
    }

    private static async Task<int> TransitionExpiredJobAsync(
        LegalAssistantDbContext db,
        JobRecord candidate,
        bool terminal,
        string error,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
        {
            return await db.Jobs
                .Where(j => j.Id == candidate.Id && j.Type == "ingest" &&
                            j.Status == JobStatus.InProgress &&
                            j.LeaseId == candidate.LeaseId &&
                            j.LeaseExpiresAt == candidate.LeaseExpiresAt &&
                            j.AttemptCount == candidate.AttemptCount &&
                            j.UpdatedAt == candidate.UpdatedAt &&
                            (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, terminal ? JobStatus.Failed : JobStatus.Queued)
                    .SetProperty(j => j.Result, terminal ? error : null)
                    .SetProperty(j => j.LastError, error)
                    .SetProperty(j => j.NextAttemptAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseId, (Guid?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);
        }

        var tracked = await db.Jobs.FirstOrDefaultAsync(j =>
            j.Id == candidate.Id && j.Type == "ingest" &&
            j.Status == JobStatus.InProgress && j.LeaseId == candidate.LeaseId &&
            j.LeaseExpiresAt == candidate.LeaseExpiresAt && j.AttemptCount == candidate.AttemptCount &&
            j.UpdatedAt == candidate.UpdatedAt && (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now), cancellationToken);
        if (tracked is null)
            return 0;

        tracked.Status = terminal ? JobStatus.Failed : JobStatus.Queued;
        tracked.Result = terminal ? error : null;
        tracked.LastError = error;
        tracked.NextAttemptAt = null;
        tracked.LeaseExpiresAt = null;
        tracked.LeaseId = null;
        tracked.UpdatedAt = now;
        return await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<int> ClearDueRetryMarkerAsync(
        LegalAssistantDbContext db,
        JobRecord candidate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
        {
            return await db.Jobs
                .Where(j => j.Id == candidate.Id && j.Type == "ingest" &&
                            j.Status == JobStatus.Queued &&
                            j.NextAttemptAt == candidate.NextAttemptAt &&
                            j.UpdatedAt == candidate.UpdatedAt &&
                            j.AttemptCount == candidate.AttemptCount &&
                            j.NextAttemptAt != null && j.NextAttemptAt <= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.NextAttemptAt, (DateTime?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);
        }

        var tracked = await db.Jobs.FirstOrDefaultAsync(j =>
            j.Id == candidate.Id && j.Type == "ingest" && j.Status == JobStatus.Queued &&
            j.NextAttemptAt == candidate.NextAttemptAt && j.UpdatedAt == candidate.UpdatedAt &&
            j.AttemptCount == candidate.AttemptCount && j.NextAttemptAt != null && j.NextAttemptAt <= now, cancellationToken);
        if (tracked is null)
            return 0;

        tracked.NextAttemptAt = null;
        tracked.UpdatedAt = now;
        return await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> ResetOrCreateOutboxAsync(
        LegalAssistantDbContext db,
        JobRecord job,
        DateTime now,
        CancellationToken cancellationToken,
        Action<OutboxMessageRecord?> captureInserted)
    {
        var deduplicationKey = job.Id.ToString("N");
        var message = await db.OutboxMessages.AsNoTracking().FirstOrDefaultAsync(
            x => x.JobId == job.Id && x.MessageType == DocumentIngestMessageNames.MessageType,
            cancellationToken);

        if (message is null)
        {
            var created = new OutboxMessageRecord
            {
                Id = Guid.NewGuid(),
                Owner = OutboxMessageOwners.Ingest,
                JobId = job.Id,
                DeduplicationKey = deduplicationKey,
                MessageType = DocumentIngestMessageNames.MessageType,
                RoutingKey = DocumentIngestMessageNames.Queue,
                Payload = job.Payload,
                CorrelationId = job.CorrelationId ?? job.Id.ToString("N"),
                Status = OutboxMessageStatus.Pending,
                Attempts = 0,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
                NextAttemptAt = now
            };
            await db.OutboxMessages.AddAsync(created, cancellationToken);
            captureInserted(created);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (message.Owner != OutboxMessageOwners.Ingest || message.DeduplicationKey != deduplicationKey)
            throw new InvalidOperationException("Ingest outbox row ownership or deduplication key is inconsistent.");

        var staleBefore = now.AddSeconds(-Math.Max(5, _options.LeaseDurationSeconds));
        if (message.Status == OutboxMessageStatus.Processing && message.UpdatedAt > staleBefore)
            return false;

        if (db.Database.IsRelational())
        {
            var updated = await db.OutboxMessages
                .Where(x => x.Id == message.Id &&
                            x.Owner == OutboxMessageOwners.Ingest &&
                            x.JobId == job.Id &&
                            x.MessageType == DocumentIngestMessageNames.MessageType &&
                            x.DeduplicationKey == deduplicationKey &&
                            x.Version == message.Version &&
                            (x.Status != OutboxMessageStatus.Processing || x.UpdatedAt <= staleBefore))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, OutboxMessageStatus.Pending)
                    .SetProperty(x => x.LeaseId, (Guid?)null)
                    .SetProperty(x => x.NextAttemptAt, now)
                    .SetProperty(x => x.LastError, (string?)null)
                    .SetProperty(x => x.PublishedAt, (DateTime?)null)
                    .SetProperty(x => x.UpdatedAt, now)
                    .SetProperty(x => x.Version, x => x.Version + 1), cancellationToken);
            return updated == 1;
        }

        var trackedOutbox = await db.OutboxMessages.FirstOrDefaultAsync(x =>
            x.Id == message.Id && x.Owner == OutboxMessageOwners.Ingest && x.Version == message.Version &&
            (x.Status != OutboxMessageStatus.Processing || x.UpdatedAt <= staleBefore), cancellationToken);
        if (trackedOutbox is null)
            return false;

        trackedOutbox.Status = OutboxMessageStatus.Pending;
        trackedOutbox.LeaseId = null;
        trackedOutbox.NextAttemptAt = now;
        trackedOutbox.LastError = null;
        trackedOutbox.PublishedAt = null;
        trackedOutbox.UpdatedAt = now;
        trackedOutbox.Version++;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> RollbackAsync(
        IDbContextTransaction? transaction,
        JobRecord candidate,
        string operation,
        int attempt,
        Exception? originalException)
    {
        if (transaction is null)
            return true;

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return true;
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(rollbackException,
                "Ingest recovery transaction rollback failed. operation={Operation} jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId} attempt={Attempt} originalException={OriginalException}",
                operation, candidate.Id, candidate.CorrelationId, candidate.LeaseId, attempt, originalException);
            if (originalException is null)
                throw new RecoveryOperationException(operation, candidate, attempt, rollbackException);
            return false;
        }
    }

    private static bool IsDeduplicationConflict(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: DeduplicationIndex
                })
                return true;
        }

        return false;
    }

    private sealed class RecoveryOperationException : Exception
    {
        public RecoveryOperationException(string operation, JobRecord job, int attempt, Exception innerException)
            : base($"Ingest recovery operation '{operation}' failed for job '{job.Id}'.", innerException)
        {
            Operation = operation;
            JobId = job.Id;
            CorrelationId = job.CorrelationId;
            Attempt = attempt;
        }

        public string Operation { get; }
        public Guid JobId { get; }
        public string? CorrelationId { get; }
        public int Attempt { get; }
    }
}
