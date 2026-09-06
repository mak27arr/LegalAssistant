using LegalAssistant.Application.Common;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class DocumentIngestOutboxRepairer : IOutboxMaintenance
{
    private const int BatchSize = 25;
    private readonly IClock _clock;

    public DocumentIngestOutboxRepairer(IClock clock)
    {
        _clock = clock;
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
            await db.OutboxMessages.AddAsync(new OutboxMessageRecord
            {
                Id = Guid.NewGuid(),
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
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
