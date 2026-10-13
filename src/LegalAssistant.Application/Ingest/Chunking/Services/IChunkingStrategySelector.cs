using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;

namespace LegalAssistant.Application.Chunking.Services;

public interface IChunkingStrategySelector
{
    // Selects a strategy for the given document context and returns both
    // the descriptor to persist and the runtime policy to use for chunking.
    (ChunkingRunDescriptor Descriptor, IChunkingPolicy Policy) Select(ChunkingRunContext context);
}
