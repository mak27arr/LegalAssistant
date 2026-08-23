namespace LegalAssistant.Application.Chunks.Models;

public sealed record DocumentChunkListItemResult(
    Guid ChunkId,
    Guid DocumentId,
    int ChunkIndex,
    string CharRange,
    string SourceUrl,
    DateTime CreatedAt,
    bool HasEmbedding,
    string Preview);
