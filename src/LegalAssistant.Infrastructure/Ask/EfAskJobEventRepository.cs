using LegalAssistant.Application.Ask;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class EfAskJobEventRepository : IAskJobEventRepository
{
    private readonly LegalAssistantDbContext _db;

    public EfAskJobEventRepository(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<AskJobEventRecord> AddAsync(AskJobEventRecord eventRecord, CancellationToken cancellationToken = default)
    {
        await _db.AskJobEvents.AddAsync(eventRecord, cancellationToken);
        return eventRecord;
    }

    public async Task<IReadOnlyList<AskJobEventRecord>> GetSinceAsync(Guid jobId, long afterEventId, CancellationToken cancellationToken = default)
        => await _db.AskJobEvents
            .Where(e => e.JobId == jobId && e.Id > afterEventId)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);

    public Task<AskJobEventRecord?> GetLatestAsync(Guid jobId, CancellationToken cancellationToken = default)
        => _db.AskJobEvents
            .Where(e => e.JobId == jobId)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
