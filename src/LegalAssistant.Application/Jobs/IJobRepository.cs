using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Jobs;

public interface IJobRepository
{
    Task AddAsync(JobRecord job, CancellationToken cancellationToken = default);
    Task<JobRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobExecutionLease?> TryMarkInProgressAsync(
        Guid id,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
    Task<bool> ReleaseInProgressAsync(
        Guid id,
        Guid leaseId,
        CancellationToken cancellationToken = default);
    Task<JobFailureResult> RecordFailureAsync(
        Guid id,
        Guid leaseId,
        string error,
        bool permanent,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}
