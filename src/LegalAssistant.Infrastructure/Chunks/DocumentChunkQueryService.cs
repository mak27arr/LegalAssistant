using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Chunks.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Chunks;

public sealed class DocumentChunkQueryService : IDocumentChunkQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly LegalAssistantDbContext _db;

    public DocumentChunkQueryService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<DocumentChunkPageResult?> GetByDocumentIdAsync(Guid documentId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = NormalizePageSize(pageSize);

        var documentExists = await _db.Documents.AsNoTracking().AnyAsync(d => d.Id == documentId && !d.IsDeleted, cancellationToken);
        if (!documentExists)
            return null;

        var query = _db.DocumentChunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(c => new DocumentChunkListItemResult(
                c.Id,
                c.DocumentId,
                c.ChunkIndex,
                c.CharRange,
                c.SourceUrl,
                c.CreatedAt,
                c.Embedding != null,
                c.Text.Length <= 30 ? c.Text : c.Text.Substring(0, 30)))
            .ToListAsync(cancellationToken);

        return new DocumentChunkPageResult(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            page < totalPages,
            page > 1 && totalPages > 0);
    }

    public async Task<DocumentChunkDetailsResult?> GetByIdAsync(Guid chunkId, CancellationToken cancellationToken = default)
        => await _db.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Id == chunkId && c.Document != null && !c.Document.IsDeleted)
            .Select(c => new DocumentChunkDetailsResult(
                c.Id,
                c.DocumentId,
                c.ChunkIndex,
                c.Text,
                c.CharRange,
                c.SourceUrl,
                c.CreatedAt,
                c.Embedding != null))
            .FirstOrDefaultAsync(cancellationToken);

    private static int NormalizePageSize(int pageSize)
        => pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
}
