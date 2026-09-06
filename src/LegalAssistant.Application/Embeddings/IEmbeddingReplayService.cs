namespace LegalAssistant.Application.Embeddings;

public interface IEmbeddingReplayService
{
    Task<bool> ReplayAsync(Guid chunkId, CancellationToken cancellationToken = default);
}
