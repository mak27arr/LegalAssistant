namespace LegalAssistant.Application.Embeddings;

public interface IEmbeddingStatusService
{
    Task<bool> MarkInProgressAsync(
        Guid chunkId,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingStatusUpdateResult> MarkCompletedAsync(
        Guid chunkId,
        float[] vector,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingStatusUpdateResult> RecordFailureAsync(
        Guid chunkId,
        string error,
        bool terminal,
        Guid? jobId,
        Guid? chunkingRunId,
        CancellationToken cancellationToken = default);

    Task<EmbeddingStatusUpdateResult> FinalizeRunAsync(
        Guid runId,
        Guid jobId,
        CancellationToken cancellationToken = default);

}
