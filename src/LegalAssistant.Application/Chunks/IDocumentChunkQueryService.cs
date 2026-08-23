using LegalAssistant.Application.Chunks.Models;

namespace LegalAssistant.Application.Chunks;

public interface IDocumentChunkQueryService
{
    Task<DocumentChunkPageResult?> GetByDocumentIdAsync(Guid documentId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<DocumentChunkDetailsResult?> GetByIdAsync(Guid chunkId, CancellationToken cancellationToken = default);
}
