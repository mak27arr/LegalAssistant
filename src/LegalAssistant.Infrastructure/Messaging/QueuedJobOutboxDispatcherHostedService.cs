using LegalAssistant.Application.Common;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class QueuedJobOutboxDispatcherHostedService : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleLease = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 25;
    private const int MaxAttempts = 10;

    private readonly IServiceProvider _sp;
    private readonly ILogger<QueuedJobOutboxDispatcherHostedService> _logger;
    private readonly IClock _clock;

    public QueuedJobOutboxDispatcherHostedService(
        IServiceProvider sp,
        ILogger<QueuedJobOutboxDispatcherHostedService> logger,
        IClock clock)
    {
        _sp = sp;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IDocumentIngestJobPublisher>();

                await RepairQueuedJobsAsync(db, stoppingToken);
                await DispatchBatchAsync(db, publisher, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher cycle failed");
            }

            await Task.Delay(PollDelay, stoppingToken);
        }
    }

    private async Task RepairQueuedJobsAsync(LegalAssistantDbContext db, CancellationToken cancellationToken)
    {
        var jobs = await db.Jobs
            .Where(j => j.Status == JobStatus.Queued)
            .Where(j => !db.OutboxMessages.Any(o => o.JobId == j.Id && o.MessageType == DocumentIngestMessageNames.MessageType))
            .OrderBy(j => j.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
            return;

        var now = _clock.UtcNow;
        foreach (var job in jobs)
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
                NextAttemptAt = job.NextAttemptAt
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchBatchAsync(LegalAssistantDbContext db, IDocumentIngestJobPublisher publisher, CancellationToken cancellationToken)
    {
        var utcNow = _clock.UtcNow;
        var messages = await ClaimDueBatchAsync(db, DocumentIngestMessageNames.MessageType, BatchSize, StaleLease, utcNow, cancellationToken);
        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.JobId, message.Payload, cancellationToken);
                await MarkPublishedAsync(db, message.Id, _clock.UtcNow, cancellationToken);
                _logger.LogInformation("Published outbox message. jobId={JobId} outboxId={OutboxId}", message.JobId, message.Id);
            }
            catch (Exception ex)
            {
                var nextAttemptAt = _clock.UtcNow.Add(CalculateBackoff(message.Attempts + 1));
                var terminal = message.Attempts + 1 >= MaxAttempts;
                await MarkRetryAsync(db, message.Id, ex.Message, nextAttemptAt, terminal, _clock.UtcNow, cancellationToken);
                _logger.LogWarning(ex, "Failed to publish outbox message. jobId={JobId} outboxId={OutboxId} attempt={Attempt}", message.JobId, message.Id, message.Attempts + 1);
            }
        }
    }

    private static async Task<IReadOnlyList<OutboxMessageRecord>> ClaimDueBatchAsync(
        LegalAssistantDbContext db,
        string messageType,
        int batchSize,
        TimeSpan staleLease,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var staleBefore = utcNow - staleLease;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.OutboxMessages
            .Where(x => x.MessageType == messageType)
            .Where(x =>
                (x.Status == OutboxMessageStatus.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow)) ||
                (x.Status == OutboxMessageStatus.Processing && x.UpdatedAt <= staleBefore))
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
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
            await tx.CommitAsync(cancellationToken);
            return messages;
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(cancellationToken);
            return [];
        }
    }

    private static async Task MarkPublishedAsync(LegalAssistantDbContext db, Guid id, DateTime utcNow, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message == null)
            return;

        message.Status = OutboxMessageStatus.Published;
        message.PublishedAt = utcNow;
        message.UpdatedAt = utcNow;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task MarkRetryAsync(LegalAssistantDbContext db, Guid id, string error, DateTime nextAttemptAt, bool isTerminal, DateTime utcNow, CancellationToken cancellationToken)
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (message == null)
            return;

        message.Attempts += 1;
        message.LastError = error;
        message.UpdatedAt = utcNow;
        message.NextAttemptAt = nextAttemptAt;
        message.Status = isTerminal ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
        message.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan CalculateBackoff(int attempt)
    {
        var multiplier = Math.Min(8, Math.Max(0, attempt - 1));
        return TimeSpan.FromSeconds(RetryBaseDelay.TotalSeconds * Math.Pow(2, multiplier));
    }
}
