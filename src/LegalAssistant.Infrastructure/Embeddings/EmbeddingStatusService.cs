using LegalAssistant.Application.Common;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs.Models;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Embeddings;

public sealed class EmbeddingStatusService : IEmbeddingStatusService
{
    public const int ExpectedEmbeddingDimensions = 768;

    private readonly LegalAssistantDbContext _db;
    private readonly IClock _clock;

    public EmbeddingStatusService(LegalAssistantDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<bool> MarkInProgressAsync(
        Guid chunkId,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default)
    {
        var chunk = await _db.DocumentChunks.FirstOrDefaultAsync(c => c.Id == chunkId, cancellationToken);
        if (chunk is null)
            return false;

        if (chunk.EmbeddingStatus == EmbeddingStatus.Completed && chunk.Embedding is not null)
            return true;

        AttachMessageContext(chunk, jobId, chunkingRunId);
        var now = _clock.UtcNow;
        chunk.EmbeddingStatus = EmbeddingStatus.InProgress;
        chunk.EmbeddingAttemptCount += 1;
        chunk.EmbeddingStartedAt = now;
        chunk.EmbeddingFailedAt = null;
        chunk.EmbeddingUpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmbeddingStatusUpdateResult> MarkCompletedAsync(
        Guid chunkId,
        float[] vector,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default)
    {
        var chunk = await _db.DocumentChunks.FirstOrDefaultAsync(c => c.Id == chunkId, cancellationToken);
        if (chunk is null)
            return MissingResult;

        if (vector.Length != ExpectedEmbeddingDimensions)
            throw new ArgumentException($"Embedding vector must contain {ExpectedEmbeddingDimensions} dimensions.", nameof(vector));

        // A duplicate completion is idempotent and completion is authoritative.
        if (chunk.EmbeddingStatus == EmbeddingStatus.Completed && chunk.Embedding is not null)
            return await RecomputeParentsAsync(chunk, jobId, chunkingRunId, null, cancellationToken);

        AttachMessageContext(chunk, jobId, chunkingRunId);
        var now = _clock.UtcNow;
        chunk.Embedding = new EmbeddingVector(vector);
        chunk.EmbeddingStatus = EmbeddingStatus.Completed;
        chunk.EmbeddingLastError = null;
        chunk.EmbeddingCompletedAt = now;
        chunk.EmbeddingFailedAt = null;
        chunk.EmbeddingUpdatedAt = now;

        return await RecomputeParentsAsync(chunk, jobId, chunkingRunId, null, cancellationToken);
    }

    public async Task<EmbeddingStatusUpdateResult> RecordFailureAsync(
        Guid chunkId,
        string error,
        bool terminal,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default)
    {
        var chunk = await _db.DocumentChunks.FirstOrDefaultAsync(c => c.Id == chunkId, cancellationToken);
        if (chunk is null)
            return MissingResult;

        // A late malformed/failing message must not regress an already persisted
        // embedding. Replays remain idempotent and completion is authoritative.
        if (chunk.EmbeddingStatus == EmbeddingStatus.Completed && chunk.Embedding is not null)
            return await RecomputeParentsAsync(chunk, jobId, chunkingRunId, null, cancellationToken);

        AttachMessageContext(chunk, jobId, chunkingRunId);
        var now = _clock.UtcNow;
        chunk.EmbeddingStatus = terminal ? EmbeddingStatus.Failed : EmbeddingStatus.Pending;
        chunk.EmbeddingLastError = error;
        chunk.EmbeddingFailedAt = terminal ? now : null;
        chunk.EmbeddingUpdatedAt = now;

        return await RecomputeParentsAsync(chunk, jobId, chunkingRunId, terminal ? error : null, cancellationToken);
    }

    public async Task<EmbeddingStatusUpdateResult> FinalizeRunAsync(
        Guid runId,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.ChunkingRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
            return MissingResult;

        var firstChunk = await _db.DocumentChunks
            .FirstOrDefaultAsync(c => c.ChunkingRunId == runId, cancellationToken);

        if (firstChunk is null)
        {
            var now = _clock.UtcNow;
            run.Status = ChunkingRunStatus.Completed;
            run.TotalChunks = 0;
            run.CompletedChunks = 0;
            run.FailedChunks = 0;
            run.EmbeddingCompletedAt = now;
            run.UpdatedAt = now;

            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
            if (job is not null)
            {
                job.Status = JobStatus.Completed;
                job.UpdatedAt = now;
                job.LeaseId = null;
                job.LeaseExpiresAt = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return new EmbeddingStatusUpdateResult(true, true, false, 0, 0, 0, job?.Status);
        }

        return await RecomputeParentsAsync(firstChunk, jobId, runId, null, cancellationToken);
    }

    private async Task<EmbeddingStatusUpdateResult> RecomputeParentsAsync(
        DocumentChunk chunk,
        Guid? messageJobId,
        Guid? messageRunId,
        string? failureError,
        CancellationToken cancellationToken)
    {
        var runId = chunk.ChunkingRunId ?? messageRunId;
        if (runId is null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new EmbeddingStatusUpdateResult(true, false, chunk.EmbeddingStatus == EmbeddingStatus.Failed, 0, 0, 0, null);
        }

        var run = await _db.ChunkingRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new EmbeddingStatusUpdateResult(true, false, chunk.EmbeddingStatus == EmbeddingStatus.Failed, 0, 0, 0, null);
        }

        var chunks = await _db.DocumentChunks
            .Where(c => c.ChunkingRunId == runId)
            .ToListAsync(cancellationToken);
        var total = chunks.Count;
        var completed = chunks.Count(c => c.EmbeddingStatus == EmbeddingStatus.Completed && c.Embedding is not null);
        var failed = chunks.Count(c => c.EmbeddingStatus == EmbeddingStatus.Failed);
        var now = _clock.UtcNow;
        var runFailed = failed > 0;
        var runCompleted = !runFailed && total == completed;

        run.TotalChunks = total;
        run.CompletedChunks = completed;
        run.FailedChunks = failed;
        run.Status = runFailed
            ? ChunkingRunStatus.Failed
            : runCompleted
                ? ChunkingRunStatus.Completed
                : ChunkingRunStatus.EmbeddingInProgress;
        run.LastError = runFailed
            ? failureError ?? chunks.FirstOrDefault(c => c.EmbeddingStatus == EmbeddingStatus.Failed)?.EmbeddingLastError
            : null;
        run.EmbeddingCompletedAt = runCompleted ? now : null;
        run.UpdatedAt = now;

        var resolvedJobId = messageJobId ?? chunk.JobId;
        var job = resolvedJobId is null
            ? null
            : await _db.Jobs.FirstOrDefaultAsync(j => j.Id == resolvedJobId, cancellationToken);

        if (job is not null && (runFailed || runCompleted))
        {
            job.Status = runFailed ? JobStatus.Failed : JobStatus.Completed;
            job.Result = IngestJobResultSerializer.Serialize(
                total,
                runFailed ? EmbeddingStatus.Failed : EmbeddingStatus.Completed);
            job.LastError = runFailed ? run.LastError : null;
            job.LeaseId = null;
            job.LeaseExpiresAt = null;
            job.NextAttemptAt = null;
            job.UpdatedAt = now;
        }
        else if (job is not null && job.Status == JobStatus.InProgress)
        {
            job.Status = JobStatus.EmbeddingInProgress;
            job.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new EmbeddingStatusUpdateResult(true, runCompleted, runFailed, total, completed, failed, job?.Status);
    }

    private static void AttachMessageContext(DocumentChunk chunk, Guid? jobId, Guid? runId)
    {
        if (chunk.JobId is null && jobId.HasValue)
            chunk.JobId = jobId;
        if (chunk.ChunkingRunId is null && runId.HasValue)
            chunk.ChunkingRunId = runId;
    }

    private static readonly EmbeddingStatusUpdateResult MissingResult =
        new(false, false, false, 0, 0, 0, null);
}
