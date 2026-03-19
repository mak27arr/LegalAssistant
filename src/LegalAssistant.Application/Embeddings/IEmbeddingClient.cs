namespace LegalAssistant.Application.Embeddings;

public interface IEmbeddingClient
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
