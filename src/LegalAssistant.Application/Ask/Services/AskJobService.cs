using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Services;

public sealed class AskJobService : IAskJobService
{
    private readonly IAskJobRepository _jobs;
    private readonly IAskJobEventRepository _events;
    private readonly IMessageOutboxWriter _outbox;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public AskJobService(
        IAskJobRepository jobs,
        IAskJobEventRepository events,
        IMessageOutboxWriter outbox,
        IUnitOfWork uow,
        IClock clock)
    {
        _jobs = jobs;
        _events = events;
        _outbox = outbox;
        _uow = uow;
        _clock = clock;
    }

    public async Task<AskJobSubmissionResult> SubmitAsync(AskJobSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Question))
            throw new ArgumentException("Question is required", nameof(command));
        if (command.TopK <= 0)
            throw new ArgumentOutOfRangeException(nameof(command), "TopK must be positive");
        if (command.OwnerUserId == Guid.Empty)
            throw new ArgumentException("OwnerUserId is required", nameof(command));
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            throw new ArgumentException("IdempotencyKey is required", nameof(command));

        var idempotencyKey = command.IdempotencyKey.Trim();
        var requestHash = ComputeRequestHash(command.Question, command.TopK, command.ConversationId);

        var existing = await _jobs.GetByIdempotencyKeyAsync(command.OwnerUserId, idempotencyKey, cancellationToken);
        if (existing != null)
        {
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Idempotency key was already used for a different ask request.");

            return new AskJobSubmissionResult(
                existing.Id,
                existing.Status,
                false,
                existing.CreatedAt,
                existing.UpdatedAt);
        }

        var now = _clock.UtcNow;
        var job = new AskJobRecord
        {
            Id = Guid.NewGuid(),
            OwnerUserId = command.OwnerUserId,
            ActorScopeKey = command.OwnerUserId.ToString("N"),
            IdempotencyKey = idempotencyKey,
            Question = command.Question.Trim(),
            TopK = command.TopK,
            ConversationId = string.IsNullOrWhiteSpace(command.ConversationId) ? null : command.ConversationId.Trim(),
            RequestHash = requestHash,
            Status = AskJobStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _jobs.AddAsync(job, cancellationToken);
        var queuedEvent = AskJobEventFactory.Create(job, AskJobStatus.Queued, now);
        await _events.AddAsync(queuedEvent, cancellationToken);
        await _outbox.AddAsync(AskJobOutboxFactory.Create(queuedEvent, now), cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new AskJobSubmissionResult(
            job.Id,
            job.Status,
            true,
            job.CreatedAt,
            job.UpdatedAt);
    }

    private static string ComputeRequestHash(string question, int topK, string? conversationId)
    {
        var normalized = JsonSerializer.Serialize(new
        {
            Question = question.Trim(),
            TopK = topK,
            ConversationId = string.IsNullOrWhiteSpace(conversationId) ? null : conversationId.Trim()
        });

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
}
