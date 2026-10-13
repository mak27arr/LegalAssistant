using LegalAssistant.Application.Common;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class DocumentIngestOutboxRepairer : IOutboxMaintenance
{
    private const int BatchSize = 25;
    private readonly IClock _clock;
    private readonly ILogger<DocumentIngestOutboxRepairer> _logger;

    public DocumentIngestOutboxRepairer(IClock clock, ILogger<DocumentIngestOutboxRepairer> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        LegalAssistantDbContext db,
        CancellationToken cancellationToken = default)
    {
        var jobs = await db.Jobs
            .Where(j => j.Status == JobStatus.Queued)
            .Where(j => !db.OutboxMessages.Any(o =>
                o.JobId == j.Id &&
                o.MessageType == DocumentIngestMessageNames.MessageType))
            .OrderBy(j => j.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
            return;

        var now = _clock.UtcNow;
        foreach (var job in jobs)
        {
            var message = new OutboxMessageRecord
            {
                Id = Guid.NewGuid(),
                Owner = OutboxMessageOwners.Ingest,
                JobId = job.Id,
                DeduplicationKey = job.Id.ToString("N"),
                MessageType = DocumentIngestMessageNames.MessageType,
                RoutingKey = DocumentIngestMessageNames.Queue,
                Payload = job.Payload,
                CorrelationId = job.CorrelationId ?? job.Id.ToString("N"),
                Status = OutboxMessageStatus.Pending,
                Attempts = 0,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now,
                NextAttemptAt = job.NextAttemptAt
            };

            IDbContextTransaction? transaction = null;
            try
            {
                if (db.Database.IsRelational())
                    transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                await db.OutboxMessages.AddAsync(message, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsDeduplicationConflict(ex))
            {
                if (transaction is not null && !await RollbackAsync(transaction, job.Id, ex))
                    throw;
                db.Entry(message).State = EntityState.Detached;

                var winnerExists = await db.OutboxMessages.AsNoTracking().AnyAsync(x =>
                    x.MessageType == DocumentIngestMessageNames.MessageType &&
                    x.DeduplicationKey == job.Id.ToString("N"), cancellationToken);
                if (!winnerExists)
                {
                    _logger.LogError(ex,
                        "Outbox deduplication constraint reported a conflict but no winning row exists. jobId={JobId} correlationId={CorrelationId} owner={Owner}",
                        job.Id, job.CorrelationId, OutboxMessageOwners.Ingest);
                    throw;
                }

                _logger.LogInformation(ex,
                    "Concurrent ingest outbox repair already inserted the deduplicated row; skipping. jobId={JobId} correlationId={CorrelationId} owner={Owner}",
                    job.Id, job.CorrelationId, OutboxMessageOwners.Ingest);
            }
            catch (Exception ex)
            {
                if (transaction is not null)
                    await RollbackAsync(transaction, job.Id, ex);
                db.Entry(message).State = EntityState.Detached;
                _logger.LogError(ex,
                    "Ingest outbox repair failed. jobId={JobId} correlationId={CorrelationId} owner={Owner}",
                    job.Id, job.CorrelationId, OutboxMessageOwners.Ingest);
                throw;
            }
            finally
            {
                if (transaction is not null)
                    await transaction.DisposeAsync();
            }
        }
    }

    private static bool IsDeduplicationConflict(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_message_outbox_message_type_deduplication_key"
                })
                return true;
        }

        return false;
    }

    private async Task<bool> RollbackAsync(IDbContextTransaction transaction, Guid jobId, Exception originalException)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return true;
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(rollbackException,
                "Rollback failed after ingest outbox repair error. jobId={JobId} originalException={OriginalException}",
                jobId, originalException);
            return false;
        }
    }
}
