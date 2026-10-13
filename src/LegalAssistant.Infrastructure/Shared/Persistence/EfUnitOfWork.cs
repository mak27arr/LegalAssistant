using LegalAssistant.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Db;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly LegalAssistantDbContext _db;

    public EfUnitOfWork(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new PersistenceConcurrencyException("The persistence state changed concurrently.", ex);
        }
    }
}
