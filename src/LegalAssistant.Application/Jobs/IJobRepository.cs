using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Jobs;

public interface IJobRepository
{
    Task AddAsync(JobRecord job, CancellationToken cancellationToken = default);
}
