using LegalAssistant.Application.Common;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Embeddings;

public sealed class EmbeddingReplayService : IEmbeddingReplayService
{
    private readonly LegalAssistantDbContext _db;
    private readonly IEmbeddingEnqueueService _publisher;
    private readonly IClock _clock;

    public EmbeddingReplayService(
        LegalAssistantDbContext db,
        IEmbeddingEnqueueService publisher,
        IClock clock)
    {
        _db = db;
        _publisher = publisher;
        _clock = clock;
    }

    public async Task<bool> ReplayAsync(Guid chunkId, CancellationToken cancellationToken = default)
    {
        var chunk = await _db.DocumentChunks.FirstOrDefaultAsync(c => c.Id == chunkId, cancellationToken);
        if (chunk is null || chunk.EmbeddingStatus == EmbeddingStatus.Completed)
            return false;

        var now = _clock.UtcNow;
        chunk.EmbeddingStatus = EmbeddingStatus.Pending;
        chunk.EmbeddingAttemptCount = 0;
        chunk.EmbeddingLastError = null;
        chunk.EmbeddingStartedAt = null;
        chunk.EmbeddingFailedAt = null;
        chunk.EmbeddingUpdatedAt = now;

        if (chunk.ChunkingRunId is Guid runId)
        {
            var run = await _db.ChunkingRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
            if (run is not null)
            {
                run.Status = ChunkingRunStatus.EmbeddingInProgress;
                run.LastError = null;
                run.UpdatedAt = now;
            }
        }

        if (chunk.JobId is Guid jobId)
        {
            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is not null && job.Status == JobStatus.Failed)
            {
                job.Status = JobStatus.EmbeddingInProgress;
                job.LastError = null;
                job.Result = null;
                job.UpdatedAt = now;
            }
        }

        await _publisher.RequeueEmbeddingAsync(
            chunk.Id,
            chunk.Text,
            chunk.JobId,
            chunk.ChunkingRunId,
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
