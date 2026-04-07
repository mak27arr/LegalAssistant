using System.Text.Json;
using System.Text.RegularExpressions;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;

namespace LegalAssistant.Infrastructure.Chunking;

public sealed class ArticleCandidate : IStrategyCandidate
{
    private const string Version = "v1";
    private const int DefaultChunkSize = 2000;
    private const int DefaultMaxChunkSize = 2000;
    private static readonly Regex ArticleRegex = new("(?m)^\\s*Стаття\\s+\\d+", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public bool CanProcess(string text) =>
        !string.IsNullOrEmpty(text) && ArticleRegex.IsMatch(text);

    public ChunkingRunDescriptor Describe()
    {
        var id = $"regex_article:{Version}";
        var parameters = new { mode = "regex_article" };
        return new ChunkingRunDescriptor(id, "regex_article", Version, JsonSerializer.Serialize(parameters));
    }

    public IChunkingPolicy CreatePolicy() =>
        new RegexOrFixedChunkingPolicy(new RegexArticleChunkingStrategy(ArticleRegex, DefaultMaxChunkSize), new FixedSizeChunkingStrategy(DefaultChunkSize));
}
