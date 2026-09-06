using LegalAssistant.Application.Chunks;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Db.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

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

    public Task<DocumentChunk?> GetByChunkingRunAndIndexAsync(
        Guid chunkingRunId,
        int chunkIndex,
        CancellationToken cancellationToken = default)
        => _db.DocumentChunks.FirstOrDefaultAsync(
            c => c.ChunkingRunId == chunkingRunId && c.ChunkIndex == chunkIndex,
            cancellationToken);

    public async Task<IReadOnlyList<DocumentChunk>> GetNearestByEmbeddingAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            return Array.Empty<DocumentChunk>();

        if (_db.Database.IsInMemory())
            return await GetNearestInMemoryAsync(queryEmbedding, topK, cancellationToken);

        var queryVector = new Vector(queryEmbedding);

        var rows = await _db.Set<DocumentChunkVectorSearchRow>()
            .Where(c => c.Embedding != null)
            .OrderBy(c => c.Embedding!.L2Distance(queryVector))
            .Take(topK)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDomain).ToList();
    }

    private async Task<IReadOnlyList<DocumentChunk>> GetNearestInMemoryAsync(
        float[] queryEmbedding,
        int topK,
        CancellationToken cancellationToken)
    {
        var rows = await _db.DocumentChunks
            .Where(c => c.Embedding != null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(c => L2Distance(c.Embedding!.Values, queryEmbedding))
            .Take(topK)
            .ToList();
    }

    private static DocumentChunk MapToDomain(DocumentChunkVectorSearchRow row)
        => new()
        {
            Id = row.Id,
            DocumentId = row.DocumentId,
            ChunkIndex = row.ChunkIndex,
            Text = row.Text,
            CharRange = row.CharRange,
            SourceUrl = row.SourceUrl,
            Embedding = row.Embedding is null
                ? null
                : new EmbeddingVector(row.Embedding.ToArray()),
            EmbeddingStatus = row.EmbeddingStatus,
            EmbeddingAttemptCount = row.EmbeddingAttemptCount,
            EmbeddingLastError = row.EmbeddingLastError,
            EmbeddingStartedAt = row.EmbeddingStartedAt,
            EmbeddingCompletedAt = row.EmbeddingCompletedAt,
            EmbeddingFailedAt = row.EmbeddingFailedAt,
            EmbeddingUpdatedAt = row.EmbeddingUpdatedAt,
            JobId = row.JobId,
            ChunkingRunId = row.ChunkingRunId,
            CreatedAt = row.CreatedAt
        };

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
