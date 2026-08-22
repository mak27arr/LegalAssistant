using LegalAssistant.Application.Chunks;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace LegalAssistant.Infrastructure.Chunks;

public sealed class EfDocumentChunkRepository : IDocumentChunkRepository
{
    private readonly LegalAssistantDbContext _db;

    public EfDocumentChunkRepository(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
        => _db.DocumentChunks.AddAsync(chunk, cancellationToken).AsTask();

    public async Task<IReadOnlyList<DocumentChunk>> GetNearestByEmbeddingAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            return Array.Empty<DocumentChunk>();

        var qv = ToVectorLiteral(queryEmbedding);

        var rows = await _db.DocumentChunks
            .FromSqlInterpolated($@"
                SELECT id, document_id, chunk_index, text, char_range, source_url, embedding, created_at
                FROM document_chunks
                WHERE embedding IS NOT NULL
                ORDER BY embedding <-> CAST({qv} AS vector(768))
                LIMIT {topK}")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows;
    }

    private static string ToVectorLiteral(float[] values)
        => "[" + string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";
}
