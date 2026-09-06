using LegalAssistant.Api.Dtos.Chunks;
using LegalAssistant.Application.Chunks.Models;

namespace LegalAssistant.Api.Mappers;

public static class ChunkMapper
{
    public static ChunkListItemDto Map(DocumentChunkListItemResult chunk)
        => new(
            chunk.ChunkId,
            chunk.DocumentId,
            chunk.ChunkIndex,
            chunk.CharRange,
            chunk.SourceUrl,
            chunk.CreatedAt,
            chunk.HasEmbedding,
            chunk.Preview,
            chunk.EmbeddingStatus,
            chunk.EmbeddingAttemptCount,
            chunk.EmbeddingLastError,
            chunk.EmbeddingStartedAt,
            chunk.EmbeddingCompletedAt,
            chunk.EmbeddingFailedAt);

    public static ChunkPageResponse Map(DocumentChunkPageResult page)
        => new(
            page.Items.Select(Map).ToList(),
            page.Page,
            page.PageSize,
            page.TotalItems,
            page.TotalPages,
            page.HasNextPage,
            page.HasPreviousPage);

    public static ChunkDetailsDto Map(DocumentChunkDetailsResult chunk)
        => new(
            chunk.ChunkId,
            chunk.DocumentId,
            chunk.ChunkIndex,
            chunk.Text,
            chunk.CharRange,
            chunk.SourceUrl,
            chunk.CreatedAt,
            chunk.HasEmbedding,
            chunk.EmbeddingStatus,
            chunk.EmbeddingAttemptCount,
            chunk.EmbeddingLastError,
            chunk.EmbeddingStartedAt,
            chunk.EmbeddingCompletedAt,
            chunk.EmbeddingFailedAt);
}
