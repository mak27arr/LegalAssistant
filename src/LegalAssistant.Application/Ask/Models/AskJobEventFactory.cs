using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Models;

public static class AskJobEventFactory
{
    public static AskJobEventRecord Create(
        AskJobRecord job,
        AskJobStatus status,
        DateTime occurredAtUtc,
        string? resultJson = null,
        string? error = null)
        => new()
        {
            JobId = job.Id,
            ActorScopeKey = job.ActorScopeKey,
            IdempotencyKey = job.IdempotencyKey,
            Question = job.Question,
            TopK = job.TopK,
            ConversationId = job.ConversationId,
            Status = status,
            ResultJson = resultJson,
            Error = error,
            OccurredAtUtc = occurredAtUtc,
            CreatedAt = occurredAtUtc
        };
}
