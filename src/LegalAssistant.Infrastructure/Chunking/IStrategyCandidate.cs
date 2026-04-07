using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;

namespace LegalAssistant.Infrastructure.Chunking
{
    public interface IStrategyCandidate
    {
        bool CanProcess(string text);
        ChunkingRunDescriptor Describe();
        IChunkingPolicy CreatePolicy();
    }
}
