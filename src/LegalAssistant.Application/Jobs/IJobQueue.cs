using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Jobs;

public interface IJobQueue
{
    Task<JobRecord?> DequeueQueuedAsync(CancellationToken cancellationToken = default);
}
