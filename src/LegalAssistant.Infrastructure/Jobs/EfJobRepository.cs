using LegalAssistant.Application.Common;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Jobs;

public sealed class EfJobRepository : IJobRepository
{
    private readonly LegalAssistantDbContext _db;
    private readonly IClock _clock;

    public EfJobRepository(LegalAssistantDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task AddAsync(JobRecord job, CancellationToken cancellationToken = default)
        => _db.Jobs.AddAsync(job, cancellationToken).AsTask();

    public Task<JobRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<JobExecutionLease?> TryMarkInProgressAsync(
        Guid id,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var leaseId = Guid.NewGuid();
        var leaseExpiresAt = now.Add(leaseDuration);

        if (IsPostgres())
        {
            var updated = await _db.Jobs
                .Where(j => j.Id == id &&
                    ((j.Status == JobStatus.Queued &&
                      (j.NextAttemptAt == null || j.NextAttemptAt <= now)) ||
                     (j.Status == JobStatus.InProgress &&
                      (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, JobStatus.InProgress)
                    .SetProperty(j => j.StartedAt, now)
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
                    .SetProperty(j => j.NextAttemptAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseExpiresAt, leaseExpiresAt)
                    .SetProperty(j => j.LeaseId, leaseId)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);

            return updated > 0 ? new JobExecutionLease(leaseId) : null;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j =>
            j.Id == id &&
            ((j.Status == JobStatus.Queued &&
              (j.NextAttemptAt == null || j.NextAttemptAt <= now)) ||
             (j.Status == JobStatus.InProgress &&
              (j.LeaseExpiresAt == null || j.LeaseExpiresAt <= now))), cancellationToken);

        if (job is null)
            return null;

        job.Status = JobStatus.InProgress;
        job.StartedAt = now;
        job.AttemptCount += 1;
        job.NextAttemptAt = null;
        job.LeaseExpiresAt = leaseExpiresAt;
        job.LeaseId = leaseId;
        job.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return new JobExecutionLease(leaseId);
    }

    public async Task<bool> ReleaseInProgressAsync(
        Guid id,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        if (IsPostgres())
        {
            var updated = await _db.Jobs
                .Where(j => j.Id == id && j.Status == JobStatus.InProgress && j.LeaseId == leaseId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, JobStatus.Queued)
                    .SetProperty(j => j.NextAttemptAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseId, (Guid?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);

            return updated > 0;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j =>
            j.Id == id && j.Status == JobStatus.InProgress && j.LeaseId == leaseId, cancellationToken);
        if (job is null)
            return false;

        job.Status = JobStatus.Queued;
        job.NextAttemptAt = null;
        job.LeaseExpiresAt = null;
        job.LeaseId = null;
        job.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<JobFailureResult> RecordFailureAsync(
        Guid id,
        Guid leaseId,
        string error,
        bool permanent,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        maxAttempts = Math.Max(1, maxAttempts);

        if (!permanent && IsPostgres())
        {
            var updated = await _db.Jobs
                .Where(j => j.Id == id &&
                            j.Status == JobStatus.InProgress &&
                            j.LeaseId == leaseId &&
                            j.AttemptCount < maxAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, JobStatus.Queued)
                    .SetProperty(j => j.Result, (string?)null)
                    .SetProperty(j => j.LastError, error)
                    .SetProperty(j => j.NextAttemptAt, now.Add(retryDelay))
                    .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseId, (Guid?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);

            if (updated > 0)
                return JobFailureResult.Retrying;
        }

        if (IsPostgres())
        {
            var updated = await _db.Jobs
                .Where(j => j.Id == id &&
                            j.Status == JobStatus.InProgress &&
                            j.LeaseId == leaseId &&
                            (permanent || j.AttemptCount >= maxAttempts))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, JobStatus.Failed)
                    .SetProperty(j => j.Result, error)
                    .SetProperty(j => j.LastError, error)
                    .SetProperty(j => j.NextAttemptAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseExpiresAt, (DateTime?)null)
                    .SetProperty(j => j.LeaseId, (Guid?)null)
                    .SetProperty(j => j.UpdatedAt, now), cancellationToken);

            return updated > 0 ? JobFailureResult.Failed : JobFailureResult.Ignored;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j =>
            j.Id == id && j.Status == JobStatus.InProgress && j.LeaseId == leaseId, cancellationToken);
        if (job is null)
            return JobFailureResult.Ignored;

        var terminal = permanent || job.AttemptCount >= maxAttempts;
        job.Status = terminal ? JobStatus.Failed : JobStatus.Queued;
        job.Result = terminal ? error : null;
        job.LastError = error;
        job.NextAttemptAt = terminal ? null : now.Add(retryDelay);
        job.LeaseExpiresAt = null;
        job.LeaseId = null;
        job.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return terminal ? JobFailureResult.Failed : JobFailureResult.Retrying;
    }

    private bool IsPostgres()
        => _db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}
