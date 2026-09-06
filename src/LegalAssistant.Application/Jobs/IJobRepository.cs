using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Jobs;

public interface IJobRepository
{
    Task AddAsync(JobRecord job, CancellationToken cancellationToken = default);
    Task<JobRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TryMarkInProgressAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ReleaseInProgressAsync(Guid id, CancellationToken cancellationToken = default);
}
