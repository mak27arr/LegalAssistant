using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Chunks;

public interface IDocumentChunkRepository
{
    Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default);
    Task<DocumentChunk?> GetByChunkingRunAndIndexAsync(Guid chunkingRunId, int chunkIndex, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentChunk>> GetNearestByEmbeddingAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default);
}
