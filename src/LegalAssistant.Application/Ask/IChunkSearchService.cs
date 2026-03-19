namespace LegalAssistant.Application.Ask;

public interface IChunkSearchService
{
    Task<IReadOnlyList<AskChunkResult>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default);
}
