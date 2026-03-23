using System.Text.Json;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Application.Chunking.Services;

namespace LegalAssistant.Infrastructure.Chunking;

public sealed class DefaultChunkingStrategySelector : IChunkingStrategySelector
{
    public ChunkingRunDescriptor Describe(ChunkingRunContext context)
    {
        // MVP: single default strategy. Can be extended to choose based on doc metadata/content type.
        var parameters = new
        {
            mode = "regex_or_fixed",
            // settings are controlled by DI-configured chunking policy factory
        };

        return new ChunkingRunDescriptor(
            StrategyName: "regex_or_fixed",
            StrategyVersion: "v1",
            ParamsJson: JsonSerializer.Serialize(parameters));
    }
}
