using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Chunking;

public sealed class EfChunkingRunRepository : IChunkingRunRepository
{
    private readonly LegalAssistantDbContext _db;

    public EfChunkingRunRepository(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(ChunkingRun run, CancellationToken cancellationToken = default)
        => _db.ChunkingRuns.AddAsync(run, cancellationToken).AsTask();

    public Task<ChunkingRun?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        => _db.ChunkingRuns.FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);
}
