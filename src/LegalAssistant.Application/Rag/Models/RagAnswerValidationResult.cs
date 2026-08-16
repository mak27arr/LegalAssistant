namespace LegalAssistant.Application.Rag.Models;

public sealed record RagAnswerValidationResult(
    bool IsValid,
    IReadOnlyList<int> CitationIds,
    IReadOnlyList<string> Issues);
