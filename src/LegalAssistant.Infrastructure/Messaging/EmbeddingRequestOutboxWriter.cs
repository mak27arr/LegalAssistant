using System.Text.Json;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class EmbeddingRequestOutboxWriter : IEmbeddingEnqueueService
{
    private readonly LegalAssistantDbContext _db;
    private readonly IMessageOutboxWriter _outbox;
    private readonly IClock _clock;

    public EmbeddingRequestOutboxWriter(
        LegalAssistantDbContext db,
        IMessageOutboxWriter outbox,
        IClock clock)
    {
        _db = db;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task EnqueueEmbeddingAsync(
        Guid chunkId,
        string text,
        Guid? jobId = null,
        Guid? chunkingRunId = null,
        CancellationToken cancellationToken = default)
    {
        if (chunkId == Guid.Empty || string.IsNullOrWhiteSpace(text))
            return;

        var deduplicationKey = GetDeduplicationKey(chunkId);
        if (await ExistsAsync(deduplicationKey, cancellationToken))
            return;

        var now = _clock.UtcNow;
        await _outbox.AddAsync(
            CreateMessage(chunkId, text, jobId, chunkingRunId, deduplicationKey, now),
            cancellationToken);
    }

    public async Task RequeueEmbeddingAsync(
        Guid chunkId,
        string text,
        Guid? jobId = null,
        Guid? chunkingRunId = null,
        CancellationToken cancellationToken = default)
    {
        if (chunkId == Guid.Empty || string.IsNullOrWhiteSpace(text))
            return;

        var deduplicationKey = GetDeduplicationKey(chunkId);
        var message = await _db.OutboxMessages.FirstOrDefaultAsync(
            x => x.MessageType == EmbeddingRequestMessageNames.MessageType &&
                 x.DeduplicationKey == deduplicationKey,
            cancellationToken);
        var now = _clock.UtcNow;

        if (message is null)
        {
            await _outbox.AddAsync(
                CreateMessage(chunkId, text, jobId, chunkingRunId, deduplicationKey, now),
                cancellationToken);
            return;
        }

        message.JobId = jobId;
        message.RoutingKey = EmbeddingRequestMessageNames.Queue;
        message.Payload = CreatePayload(chunkId, text, jobId, chunkingRunId);
        message.CorrelationId = (jobId ?? chunkId).ToString("N");
        message.Status = OutboxMessageStatus.Pending;
        message.Attempts = 0;
        message.NextAttemptAt = now;
        message.LastError = null;
        message.PublishedAt = null;
        message.UpdatedAt = now;
        message.Version += 1;
    }

    private async Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken)
    {
        var tracked = _db.ChangeTracker.Entries<OutboxMessageRecord>()
            .Any(x => x.State != EntityState.Deleted &&
                      x.Entity.MessageType == EmbeddingRequestMessageNames.MessageType &&
                      x.Entity.DeduplicationKey == deduplicationKey);
        if (tracked)
            return true;

        return await _db.OutboxMessages.AnyAsync(
            x => x.MessageType == EmbeddingRequestMessageNames.MessageType &&
                 x.DeduplicationKey == deduplicationKey,
            cancellationToken);
    }

    private static OutboxMessageRecord CreateMessage(
        Guid chunkId,
        string text,
        Guid? jobId,
        Guid? chunkingRunId,
        string deduplicationKey,
        DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            DeduplicationKey = deduplicationKey,
            MessageType = EmbeddingRequestMessageNames.MessageType,
            RoutingKey = EmbeddingRequestMessageNames.Queue,
            Payload = CreatePayload(chunkId, text, jobId, chunkingRunId),
            CorrelationId = (jobId ?? chunkId).ToString("N"),
            Status = OutboxMessageStatus.Pending,
            Attempts = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
            NextAttemptAt = now
        };

    private static string CreatePayload(Guid chunkId, string text, Guid? jobId, Guid? chunkingRunId)
        => JsonSerializer.Serialize(new { chunkId, text, jobId, chunkingRunId });

    private static string GetDeduplicationKey(Guid chunkId)
        => chunkId.ToString("N");
}
