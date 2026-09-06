using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

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

        if (_db.Database.IsInMemory())
            return await SearchInMemoryAsync(queryEmbedding, topK, cancellationToken);

        var queryVector = new Vector(queryEmbedding);

        var rows = await _db.Set<LegalAssistant.Infrastructure.Db.Models.DocumentChunkVectorSearchRow>()
            .Join(
                _db.Documents.Where(d => !d.IsDeleted),
                chunk => chunk.DocumentId,
                document => document.Id,
                (chunk, document) => new { chunk, document })
            .Where(x => x.chunk.Embedding != null
                        && x.chunk.ChunkingRunId == x.document.ActiveChunkingRunId)
            .OrderBy(x => x.chunk.Embedding!.L2Distance(queryVector))
            .Take(topK)
            .Select(x => new
            {
                x.chunk.Id,
                x.chunk.DocumentId,
                x.chunk.ChunkIndex,
                x.chunk.Text,
                x.chunk.SourceUrl,
                Score = x.chunk.Embedding!.L2Distance(queryVector)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new AskChunkResult(r.Id, r.DocumentId, r.ChunkIndex, r.Text, r.SourceUrl, r.Score))
            .ToList();
    }

    private async Task<IReadOnlyList<AskChunkResult>> SearchInMemoryAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var rows = await _db.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Embedding != null)
            .Join(
                _db.Documents.Where(d => !d.IsDeleted),
                chunk => chunk.DocumentId,
                document => document.Id,
                (chunk, document) => new { chunk, document })
            .Where(x => x.chunk.ChunkingRunId == x.document.ActiveChunkingRunId)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new
            {
                x.chunk,
                Score = L2Distance(x.chunk.Embedding!.Values, queryEmbedding)
            })
            .OrderBy(x => x.Score)
            .Take(topK)
            .Select(x => new AskChunkResult(
                x.chunk.Id,
                x.chunk.DocumentId,
                x.chunk.ChunkIndex,
                x.chunk.Text,
                x.chunk.SourceUrl,
                x.Score))
            .ToList();
    }

    private static float L2Distance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var dimensions = Math.Min(left.Count, right.Count);
        var sum = 0f;
        for (var i = 0; i < dimensions; i++)
        {
            var difference = left[i] - right[i];
            sum += difference * difference;
        }

        return MathF.Sqrt(sum);
    }

}
