namespace LegalAssistant.Application.Ask;

public sealed record AskChunkResult(
    Guid ChunkId,
    Guid DocumentId,
    int ChunkIndex,
    string Text,
    string? SourceUrl,
    double Score);

public sealed record AskResult(
    string Question,
    int TopK,
    IReadOnlyList<AskChunkResult> Chunks);
