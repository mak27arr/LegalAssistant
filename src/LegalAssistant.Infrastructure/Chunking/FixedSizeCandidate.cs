using System.Text.Json;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;

namespace LegalAssistant.Infrastructure.Chunking;

public sealed class FixedSizeCandidate : IStrategyCandidate
{
    private const string Version = "v1";
    private const int DefaultChunkSize = 2000;

    public bool CanProcess(string text) => true;

    public ChunkingRunDescriptor Describe()
    {
        var id = $"fixed_size:{Version}";
        var parameters = new { mode = "fixed_size" };
        return new ChunkingRunDescriptor(id, "fixed_size", Version, JsonSerializer.Serialize(parameters));
    }

    public IChunkingPolicy CreatePolicy() =>
        new RegexOrFixedChunkingPolicy(new FixedSizeChunkingStrategy(DefaultChunkSize), new FixedSizeChunkingStrategy(DefaultChunkSize));
}
