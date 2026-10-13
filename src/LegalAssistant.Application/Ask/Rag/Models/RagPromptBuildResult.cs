using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Rag.Models;

public sealed record RagPromptBuildResult(
    string Prompt,
    IReadOnlyList<RagAnswerSource> Sources,
    int RequestedTopK,
    int EffectiveTopK,
    int UsedChunkCount,
    int PromptTokenBudget,
    int PromptTokenEstimate,
    bool WasTruncatedByBudget);
