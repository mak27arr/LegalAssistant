using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Ask.Models;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Application.Ask;

public sealed class AskService : IAskService
{
    private readonly IEmbeddingClient _embeddings;
    private readonly IChunkSearchService _search;
    private readonly ILogger<AskService> _logger;

    public AskService(IEmbeddingClient embeddings, IChunkSearchService search, ILogger<AskService> logger)
    {
        _embeddings = embeddings;
        _search = search;
        _logger = logger;
    }

    public async Task<Models.AskResult> AskAsync(Models.AskQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
            throw new ArgumentException("Question is required", nameof(query));

        var topK = query.TopK <= 0 ? 5 : query.TopK;
        var embedding = await _embeddings.GetEmbeddingAsync(query.Question, cancellationToken);

        var chunks = await _search.SearchAsync(embedding, topK, cancellationToken);
        if (chunks.Count == 0)
        {
            _logger.LogWarning(
                "Ask retrieval returned zero chunks. Question='{Question}' TopK={TopK}",
                query.Question,
                topK);
        }

        return new Models.AskResult(query.Question, topK, chunks);
    }
}
