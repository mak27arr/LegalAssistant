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

    public async Task<bool> TryMarkInProgressAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var updated = await _db.Jobs
                .Where(j => j.Id == id && j.Status == JobStatus.Queued)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, JobStatus.InProgress)
                    .SetProperty(j => j.UpdatedAt, _clock.UtcNow), cancellationToken);

            return updated > 0;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job == null || job.Status != JobStatus.Queued)
            return false;

        job.Status = JobStatus.InProgress;
        job.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReleaseInProgressAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var updated = await _db.Jobs
                .Where(j => j.Id == id && j.Status == JobStatus.InProgress)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, JobStatus.Queued)
                    .SetProperty(j => j.UpdatedAt, _clock.UtcNow), cancellationToken);

            return updated > 0;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null || job.Status != JobStatus.InProgress)
            return false;

        job.Status = JobStatus.Queued;
        job.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
