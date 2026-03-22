using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
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
            .SqlQuery<SearchRow>($@"
                SELECT id AS Id,
                       document_id AS DocumentId,
                       chunk_index AS ChunkIndex,
                       text AS Text,
                       source_url AS SourceUrl,
                       (embedding <-> {qv})::double precision AS Score
                FROM document_chunks
                WHERE embedding IS NOT NULL
                ORDER BY embedding <-> {qv}
                LIMIT {topK}")
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new AskChunkResult(r.Id, r.DocumentId, r.ChunkIndex, r.Text, r.SourceUrl, r.Score))
            .ToList();
    }

    private sealed class SearchRow
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public int ChunkIndex { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        public double Score { get; set; }
    }
}
