using LegalAssistant.Application.Jobs;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;

namespace LegalAssistant.Infrastructure.Jobs;

public sealed class EfJobRepository : IJobRepository
{
    private readonly LegalAssistantDbContext _db;

    public EfJobRepository(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(JobRecord job, CancellationToken cancellationToken = default)
        => _db.Jobs.AddAsync(job, cancellationToken).AsTask();
}
