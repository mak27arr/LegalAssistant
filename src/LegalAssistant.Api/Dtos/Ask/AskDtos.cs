namespace LegalAssistant.Api.Dtos.Ask;

public sealed record AskRequest(string Question, int? TopK);

public sealed record AskAsyncRequest(string Question, int? TopK, string? ConversationId);

public sealed record AskChunkDto(Guid ChunkId, Guid DocumentId, int ChunkIndex, string Text, string? SourceUrl, double Score);

public sealed record AskResponse(
    string Question,
    string Answer,
    bool IsGrounded);

public sealed record AskPromptResponse(
    string Question,
    int TopK,
    string Prompt,
    IReadOnlyList<AskChunkDto> Chunks);

public sealed record AskJobResponse(
    Guid JobId,
    string Status,
    string Question,
    int TopK,
    string? ConversationId,
    string? Error,
    AskResponse? Result,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AskJobSubmissionResponse(
    Guid JobId,
    string Status,
    bool IsNew,
    DateTime CreatedAt,
    DateTime UpdatedAt);
