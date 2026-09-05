using LegalAssistant.Application.Ask;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class EfAskJobRepository : IAskJobRepository
{
    private readonly LegalAssistantDbContext _db;
    private readonly LegalAssistant.Application.Common.IClock _clock;

    public EfAskJobRepository(LegalAssistantDbContext db, LegalAssistant.Application.Common.IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task AddAsync(AskJobRecord job, CancellationToken cancellationToken = default)
        => _db.AskJobs.AddAsync(job, cancellationToken).AsTask();

    public Task<AskJobRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.AskJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<AskJobRecord?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default)
        => _db.AskJobs.FirstOrDefaultAsync(j => j.Id == id && j.OwnerUserId == ownerUserId, cancellationToken);

    public Task<AskJobRecord?> GetByIdempotencyKeyAsync(Guid ownerUserId, string idempotencyKey, CancellationToken cancellationToken = default)
        => _db.AskJobs.FirstOrDefaultAsync(j => j.OwnerUserId == ownerUserId && j.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<AskJobRecord?> DequeueQueuedAsync(CancellationToken cancellationToken = default)
        => _db.AskJobs
            .Where(j => j.Status == AskJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryMarkInProgressAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var updated = await _db.AskJobs
                .Where(j => j.Id == id && j.Status == AskJobStatus.Queued)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, AskJobStatus.InProgress)
                    .SetProperty(j => j.UpdatedAt, _clock.UtcNow), cancellationToken);

            return updated > 0;
        }

        var job = await _db.AskJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job == null || job.Status != AskJobStatus.Queued)
            return false;

        job.Status = AskJobStatus.InProgress;
        job.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
