using LegalAssistant.Application.Chunking.Models;

namespace LegalAssistant.Application.Chunking.Services;

public interface IChunkingStrategySelector
{
    ChunkingRunDescriptor Describe(ChunkingRunContext context);
}
