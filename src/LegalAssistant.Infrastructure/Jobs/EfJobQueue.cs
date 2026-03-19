using LegalAssistant.Application.Jobs;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Jobs;

public sealed class EfJobQueue : IJobQueue
{
    private readonly LegalAssistantDbContext _db;

    public EfJobQueue(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public Task<JobRecord?> DequeueQueuedAsync(CancellationToken cancellationToken = default)
        => _db.Jobs.FirstOrDefaultAsync(j => j.Status == JobStatus.Queued, cancellationToken);
}
