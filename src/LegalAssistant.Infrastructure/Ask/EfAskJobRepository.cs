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

    public async Task<AskJobExecutionLease?> TryClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var candidates = await _db.AskJobs
            .AsNoTracking()
            .Where(j => j.Status == AskJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .ThenBy(j => j.Id)
            .Select(j => j.Id)
            .Take(25)
            .ToListAsync(cancellationToken);

        foreach (var candidateId in candidates)
        {
            var leaseId = Guid.NewGuid();
            var leaseExpiresAt = now.Add(leaseDuration);
            if (_db.Database.IsRelational())
            {
                var updated = await _db.AskJobs
                    .Where(j => j.Id == candidateId && j.Status == AskJobStatus.Queued)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(j => j.Status, AskJobStatus.InProgress)
                        .SetProperty(j => j.StartedAt, now)
                        .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
                        .SetProperty(j => j.LeaseExpiresAt, leaseExpiresAt)
                        .SetProperty(j => j.LeaseId, leaseId)
                        .SetProperty(j => j.UpdatedAt, now), cancellationToken);
                if (updated == 1)
                    return new AskJobExecutionLease(candidateId, leaseId);
                continue;
            }

            var candidate = await _db.AskJobs.FirstOrDefaultAsync(j =>
                j.Id == candidateId && j.Status == AskJobStatus.Queued, cancellationToken);
            if (candidate is null)
                continue;

            ApplyClaim(candidate, leaseId, now, leaseExpiresAt);
            await _db.SaveChangesAsync(cancellationToken);
            return new AskJobExecutionLease(candidateId, leaseId);
        }

        return null;
    }

    public async Task<AskJobExecutionLease?> TryClaimAsync(Guid id, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var leaseId = Guid.NewGuid();
        var leaseExpiresAt = now.Add(leaseDuration);

        if (_db.Database.IsRelational())
        {
            var updated = await _db.AskJobs
                .Where(j => j.Id == id &&
                    (j.Status == AskJobStatus.Queued ||
                     (j.Status == AskJobStatus.InProgress &&
                      (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, AskJobStatus.InProgress)
                    .SetProperty(j => j.StartedAt, now)
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
                    .SetProperty(j => j.LeaseExpiresAt, leaseExpiresAt)
                    .SetProperty(j => j.LeaseId, leaseId)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);

            return updated > 0 ? new AskJobExecutionLease(id, leaseId) : null;
        }

        var job = await _db.AskJobs.FirstOrDefaultAsync(j =>
            j.Id == id &&
            (j.Status == AskJobStatus.Queued ||
             (j.Status == AskJobStatus.InProgress &&
              (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))), cancellationToken);
        if (job is null)
            return null;

        ApplyClaim(job, leaseId, now, leaseExpiresAt);
        await _db.SaveChangesAsync(cancellationToken);
        return new AskJobExecutionLease(id, leaseId);
    }

    public async Task<bool> RenewLeaseAsync(Guid id, Guid leaseId, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var leaseExpiresAt = now.Add(leaseDuration);

        if (IsPostgres())
        {
            var updated = await _db.AskJobs
                .Where(j => j.Id == id && j.Status == AskJobStatus.InProgress && j.LeaseId == leaseId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.LeaseExpiresAt, leaseExpiresAt)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);
            return updated > 0;
        }

        var job = await _db.AskJobs.FirstOrDefaultAsync(j =>
            j.Id == id && j.Status == AskJobStatus.InProgress && j.LeaseId == leaseId, cancellationToken);
        if (job is null)
            return false;

        job.LeaseExpiresAt = leaseExpiresAt;
        job.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReleaseInProgressAsync(Guid id, Guid leaseId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        if (IsPostgres())
        {
            var updated = await _db.AskJobs
                .Where(j => j.Id == id && j.Status == AskJobStatus.InProgress && j.LeaseId == leaseId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, AskJobStatus.Queued)
                    .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseId, (Guid?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);
            return updated > 0;
        }

        var job = await _db.AskJobs.FirstOrDefaultAsync(j =>
            j.Id == id && j.Status == AskJobStatus.InProgress && j.LeaseId == leaseId, cancellationToken);
        if (job is null)
            return false;

        job.Status = AskJobStatus.Queued;
        job.LeaseExpiresAt = null;
        job.LeaseId = null;
        job.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RecoverExpiredAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var jobs = await _db.AskJobs
            .AsNoTracking()
            .Where(j => j.Status == AskJobStatus.InProgress &&
                        (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
            .OrderBy(j => j.LeaseExpiresAt)
            .ThenBy(j => j.UpdatedAt)
            .Take(Math.Clamp(batchSize, 1, 200))
            .Select(j => new { j.Id, j.LeaseId })
            .ToListAsync(cancellationToken);

        var recovered = 0;
        foreach (var job in jobs)
        {
            if (!IsPostgres())
            {
                var tracked = await _db.AskJobs.FirstOrDefaultAsync(j =>
                    j.Id == job.Id &&
                    j.Status == AskJobStatus.InProgress &&
                    j.LeaseId == job.LeaseId &&
                    (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now), cancellationToken);
                if (tracked is null)
                    continue;

                tracked.Status = AskJobStatus.Queued;
                tracked.LeaseExpiresAt = null;
                tracked.LeaseId = null;
                tracked.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                recovered++;
                continue;
            }

            recovered += await _db.AskJobs
                .Where(j => j.Id == job.Id &&
                            j.Status == AskJobStatus.InProgress &&
                            j.LeaseId == job.LeaseId &&
                            (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, AskJobStatus.Queued)
                    .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseId, (Guid?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);
        }

        return recovered;
    }

    private static void ApplyClaim(AskJobRecord job, Guid leaseId, DateTime now, DateTime leaseExpiresAt)
    {
        job.Status = AskJobStatus.InProgress;
        job.StartedAt = now;
        job.AttemptCount += 1;
        job.LeaseExpiresAt = leaseExpiresAt;
        job.LeaseId = leaseId;
        job.UpdatedAt = now;
    }

    private bool IsPostgres()
        => _db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}
