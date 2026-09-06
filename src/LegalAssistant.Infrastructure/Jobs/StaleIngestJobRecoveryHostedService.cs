using LegalAssistant.Application.Common;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Jobs;

public sealed class StaleIngestJobRecoveryHostedService : BackgroundService
{
    private const string LeaseExpiredMessage = "Ingest job lease expired before completion.";

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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stale ingest job recovery cycle failed");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.RecoveryIntervalSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task RecoverBatchAsync(
        LegalAssistantDbContext db,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        var batchSize = Math.Clamp(_options.RecoveryBatchSize, 1, 200);

        var jobs = await db.Jobs
            .AsNoTracking()
            .Where(j => j.Type == "ingest" &&
                        j.Status == JobStatus.InProgress &&
                        (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
            .OrderBy(j => j.LeaseExpiresAt)
            .ThenBy(j => j.UpdatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            var error = job.LastError ?? LeaseExpiredMessage;
            var terminal = job.AttemptCount >= maxAttempts;

            var updated = await db.Jobs
                .Where(j => j.Id == job.Id &&
                            j.Status == JobStatus.InProgress &&
                            j.LeaseId == job.LeaseId &&
                            (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, terminal ? JobStatus.Failed : JobStatus.Queued)
                    .SetProperty(j => j.Result, terminal ? error : null)
                    .SetProperty(j => j.LastError, error)
                    .SetProperty(j => j.NextAttemptAt, terminal ? null : now)
                    .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseId, (Guid?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);

            if (updated == 0)
                continue;

            if (terminal)
            {
                _logger.LogError(
                    "Stale ingest job exceeded the retry limit and was failed. jobId={JobId} correlationId={CorrelationId} attempt={Attempt}",
                    job.Id,
                    job.CorrelationId ?? job.Id.ToString("N"),
                    job.AttemptCount);
                continue;
            }

            await RequeueOutboxMessageAsync(db, job, now, cancellationToken);
            _logger.LogWarning(
                "Requeued stale ingest job after lease expiry. jobId={JobId} correlationId={CorrelationId} attempt={Attempt}",
                job.Id,
                job.CorrelationId ?? job.Id.ToString("N"),
                job.AttemptCount);
        }

        // If the process stops between the job update and outbox update, the
        // existing queued-job repair in the dispatcher recreates the message.
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task RequeueOutboxMessageAsync(
        LegalAssistantDbContext db,
        JobRecord job,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(
            x => x.JobId == job.Id && x.MessageType == DocumentIngestMessageNames.MessageType,
            cancellationToken);

        if (message is null)
        {
            await db.OutboxMessages.AddAsync(new OutboxMessageRecord
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
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
            }, cancellationToken);
            return;
        }

        message.Status = OutboxMessageStatus.Pending;
        message.Attempts = 0;
        message.NextAttemptAt = now;
        message.LastError = null;
        message.PublishedAt = null;
        message.UpdatedAt = now;
        message.Version += 1;
    }

    private async Task RepairDueQueuedJobsAsync(
        LegalAssistantDbContext db,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var jobs = await db.Jobs
            .AsNoTracking()
            .Where(j => j.Type == "ingest" &&
                        j.Status == JobStatus.Queued &&
                        j.NextAttemptAt != null &&
                        j.NextAttemptAt <= now)
            .OrderBy(j => j.NextAttemptAt)
            .Take(Math.Clamp(_options.RecoveryBatchSize, 1, 200))
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            await RequeueOutboxMessageAsync(db, job, now, cancellationToken);

            // The outbox message now owns the retry schedule. Clearing this field
            // prevents the same published message from being reset on every cycle.
            await db.Jobs
                .Where(j => j.Id == job.Id &&
                            j.Status == JobStatus.Queued &&
                            j.NextAttemptAt != null &&
                            j.NextAttemptAt <= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.NextAttemptAt, (DateTime?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
