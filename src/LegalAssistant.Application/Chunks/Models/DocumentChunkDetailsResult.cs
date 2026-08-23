namespace LegalAssistant.Application.Chunks.Models;

public sealed record DocumentChunkDetailsResult(
    Guid ChunkId,
    Guid DocumentId,
    int ChunkIndex,
    string Text,
    string CharRange,
    string SourceUrl,
    DateTime CreatedAt,
    bool HasEmbedding);
