using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Domain.Chunking;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Chunking.Services;

public interface IChunkingRunService
{
    Task<(ChunkingRun Run, IChunkingPolicy Policy)> CreateAsync(ChunkingRunContext context, CancellationToken cancellationToken = default);
}
