using LegalAssistant.Application.Persistence;

namespace LegalAssistant.Infrastructure.Db;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly LegalAssistantDbContext _db;

    public EfUnitOfWork(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
