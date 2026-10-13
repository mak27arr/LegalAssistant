namespace LegalAssistant.Application.Rag.Models;

public sealed record RagAnswerQuery(string Question, int TopK);

public sealed record RagAnswerSource(
    Guid ChunkId,
    Guid DocumentId,
    int ChunkIndex,
    string Text,
    string? SourceUrl,
    double Score);

public sealed record RagAnswerResult(
    string Question,
    int RequestedTopK,
    int TopK,
    int UsedChunkCount,
    string Answer,
    IReadOnlyList<RagAnswerSource> Sources,
    string Prompt,
    int PromptTokenBudget,
    int PromptTokenEstimate,
    bool WasTruncatedByBudget,
    bool IsGrounded,
    IReadOnlyList<int> CitationIds,
    IReadOnlyList<string> ValidationIssues);

public sealed record RagPromptResult(
    string Question,
    int RequestedTopK,
    int TopK,
    int UsedChunkCount,
    IReadOnlyList<RagAnswerSource> Sources,
    string Prompt,
    int PromptTokenBudget,
    int PromptTokenEstimate,
    bool WasTruncatedByBudget);
