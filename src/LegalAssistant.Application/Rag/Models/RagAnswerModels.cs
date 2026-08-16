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
    int TopK,
    string Answer,
    IReadOnlyList<RagAnswerSource> Sources,
    string Prompt,
    bool IsGrounded,
    IReadOnlyList<int> CitationIds,
    IReadOnlyList<string> ValidationIssues);

public sealed record RagPromptResult(
    string Question,
    int TopK,
    IReadOnlyList<RagAnswerSource> Sources,
    string Prompt);
