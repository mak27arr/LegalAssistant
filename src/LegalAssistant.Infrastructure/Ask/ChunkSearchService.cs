using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Infrastructure.Ask.Models;
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
        var rows = await _db.Database
            .SqlQuery<ChunkSearchProjection>($@"
                SELECT dc.id AS Id,
                       dc.document_id AS DocumentId,
                       dc.chunk_index AS ChunkIndex,
                       dc.text AS Text,
                       dc.source_url AS SourceUrl,
                       (dc.embedding <-> {qv})::double precision AS Score
                FROM document_chunks dc
                INNER JOIN documents d ON dc.document_id = d.id
                WHERE dc.embedding IS NOT NULL 
                  AND d.is_deleted = false 
                  AND dc.chunking_run_id = d.active_chunking_run_id
                ORDER BY dc.embedding <-> {qv}
                LIMIT {topK}")
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new AskChunkResult(r.Id, r.DocumentId, r.ChunkIndex, r.Text, r.SourceUrl, r.Score))
            .ToList();
    }
}
