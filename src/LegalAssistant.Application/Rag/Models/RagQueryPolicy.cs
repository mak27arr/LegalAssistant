namespace LegalAssistant.Application.Rag.Models;

public sealed record RagQueryPolicy(
    int DefaultTopK,
    int MaxTopK,
    int PromptTokenBudget,
    int ApproxCharsPerToken);
