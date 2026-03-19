using LegalAssistant.Application.Ask;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class ChunkSearchService : IChunkSearchService
{
    private readonly LegalAssistantDbContext _db;

    public ChunkSearchService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AskChunkResult>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            return Array.Empty<AskChunkResult>();

        var qv = new Vector(queryEmbedding);

        // Use pgvector operator directly for ordering: embedding <-> query
        // NOTE: score here is L2 distance (lower is better).
        var rows = await _db.DocumentChunks
            .FromSqlInterpolated($@"
                SELECT id, document_id, chunk_index, text, char_range, source_url, embedding, created_at
                FROM document_chunks
                WHERE embedding IS NOT NULL
                ORDER BY embedding <-> {qv}
                LIMIT {topK}")
            .AsNoTracking()
            .Select(c => new AskChunkResult(
                c.Id,
                c.DocumentId,
                c.ChunkIndex,
                c.Text,
                c.SourceUrl,
                0))
            .ToListAsync(cancellationToken);

        return rows;
    }
}
