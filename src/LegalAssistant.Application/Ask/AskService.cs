using LegalAssistant.Application.Embeddings;

namespace LegalAssistant.Application.Ask;

public sealed class AskService : IAskService
{
    private readonly IEmbeddingClient _embeddings;
    private readonly IChunkSearchService _search;

    public AskService(IEmbeddingClient embeddings, IChunkSearchService search)
    {
        _embeddings = embeddings;
        _search = search;
    }

    public async Task<AskResult> AskAsync(AskQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
            throw new ArgumentException("Question is required", nameof(query));

        var topK = query.TopK <= 0 ? 5 : query.TopK;
        var embedding = await _embeddings.GetEmbeddingAsync(query.Question, cancellationToken);

        var chunks = await _search.SearchAsync(embedding, topK, cancellationToken);
        return new AskResult(query.Question, topK, chunks);
    }
}
