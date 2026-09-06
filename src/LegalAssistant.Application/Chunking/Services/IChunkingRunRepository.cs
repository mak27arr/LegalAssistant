using System;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Chunking.Services;

public interface IChunkingRunRepository
{
    Task AddAsync(ChunkingRun run, CancellationToken cancellationToken = default);
    Task<ChunkingRun?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}
