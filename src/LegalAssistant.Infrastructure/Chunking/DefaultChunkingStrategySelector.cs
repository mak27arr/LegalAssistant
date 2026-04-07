using System.Text.Json;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Domain.Chunking;

namespace LegalAssistant.Infrastructure.Chunking;

public sealed class DefaultChunkingStrategySelector : IChunkingStrategySelector
{
    private const string Version = "v1";

    private readonly IEnumerable<IStrategyCandidate> _candidates;

    public DefaultChunkingStrategySelector(IEnumerable<IStrategyCandidate> candidates)
    {
        _candidates = candidates ?? Enumerable.Empty<IStrategyCandidate>();
    }

    public (ChunkingRunDescriptor Descriptor, IChunkingPolicy Policy) Select(ChunkingRunContext context)
    {
        var text = context.Text ?? string.Empty;

        var candidate = _candidates.FirstOrDefault(c => c.CanProcess(text));
        if (candidate is not null)
            return (candidate.Describe(), candidate.CreatePolicy());

        var fixedCandidate = _candidates.FirstOrDefault(c => c.Describe().StrategyName == "fixed_size");
        if (fixedCandidate is not null)
            return (fixedCandidate.Describe(), fixedCandidate.CreatePolicy());

        var fallbackParams = new { mode = "fixed_size" };
        var fallbackDesc = new ChunkingRunDescriptor($"fixed_size:{Version}", "fixed_size", Version, JsonSerializer.Serialize(fallbackParams));
        var fallbackPolicy = new RegexOrFixedChunkingPolicy(new FixedSizeChunkingStrategy(), new FixedSizeChunkingStrategy());
        return (fallbackDesc, fallbackPolicy);
    }
}
