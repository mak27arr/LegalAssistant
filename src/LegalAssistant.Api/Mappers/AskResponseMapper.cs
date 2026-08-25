using LegalAssistant.Api.Dtos.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Domain.Models;
using System.Text.Json;

namespace LegalAssistant.Api.Mappers;

public static class AskResponseMapper
{
    public static AskJobResponse Map(AskJobDetails job)
        => Map(job.JobId, job.Status, job.ActorScopeKey, job.IdempotencyKey, job.Question, job.TopK, job.ConversationId, job.Error, job.Result, job.CreatedAt, job.UpdatedAt);

    public static AskJobResponse Map(AskJobEventRecord eventRecord)
        => Map(
            eventRecord.JobId,
            eventRecord.Status,
            eventRecord.ActorScopeKey,
            eventRecord.IdempotencyKey,
            eventRecord.Question,
            eventRecord.TopK,
            eventRecord.ConversationId,
            eventRecord.Error,
            string.IsNullOrWhiteSpace(eventRecord.ResultJson) ? null : JsonSerializer.Deserialize<LegalAssistant.Application.Rag.Models.RagAnswerResult>(eventRecord.ResultJson),
            eventRecord.CreatedAt,
            eventRecord.OccurredAtUtc);

    private static AskJobResponse Map(
        Guid jobId,
        AskJobStatus status,
        string actorScopeKey,
        string idempotencyKey,
        string question,
        int topK,
        string? conversationId,
        string? error,
        LegalAssistant.Application.Rag.Models.RagAnswerResult? result,
        DateTime createdAt,
        DateTime updatedAt)
        => new(
            jobId,
            status.ToString(),
            actorScopeKey,
            idempotencyKey,
            question,
            topK,
            conversationId,
            error,
            result == null ? null : Map(result),
            createdAt,
            updatedAt);

    private static AskResponse Map(LegalAssistant.Application.Rag.Models.RagAnswerResult result)
        => new(
            result.Question,
            result.Answer,
            result.IsGrounded);
}
