using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Models;

public sealed record AskJobSubmissionCommand(
    string Question,
    int TopK,
    string? ConversationId,
    Guid OwnerUserId,
    string IdempotencyKey);

public sealed record AskJobSubmissionResult(
    Guid JobId,
    AskJobStatus Status,
    bool IsNew,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AskJobDetails(
    Guid JobId,
    AskJobStatus Status,
    string Question,
    int TopK,
    string? ConversationId,
    string? Error,
    RagAnswerResult? Result,
    DateTime CreatedAt,
    DateTime UpdatedAt);
