using System.Text.Json;
using System.Text.RegularExpressions;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;

namespace LegalAssistant.Infrastructure.Chunking;

public sealed class NumberedSectionCandidate : IStrategyCandidate
{
    private const string Version = "v1";
    private const int DefaultChunkSize = 2000;
    private const int DefaultMaxChunkSize = 2000;
    private static readonly Regex NumberedRegex = new("(?m)^\\s*\\d+(?:\\.\\d+)*\\s+", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public bool CanProcess(string text) =>
        !string.IsNullOrEmpty(text) && NumberedRegex.IsMatch(text);

    public ChunkingRunDescriptor Describe()
    {
        var id = $"regex_numbered_sections:{Version}";
        var parameters = new { mode = "regex_numbered_sections" };
        return new ChunkingRunDescriptor(id, "regex_numbered_sections", Version, JsonSerializer.Serialize(parameters));
    }

    public IChunkingPolicy CreatePolicy() =>
        new RegexOrFixedChunkingPolicy(new RegexNumberedSectionChunkingStrategy(NumberedRegex, DefaultMaxChunkSize), new FixedSizeChunkingStrategy(DefaultChunkSize));
}
