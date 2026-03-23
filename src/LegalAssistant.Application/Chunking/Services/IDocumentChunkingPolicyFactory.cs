using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;

namespace LegalAssistant.Application.Chunking.Services;

public interface IDocumentChunkingPolicyFactory
{
    IChunkingPolicy Create(ChunkingRunDescriptor descriptor);
}
