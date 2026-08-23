using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobEventRepository
{
    Task<AskJobEventRecord> AddAsync(AskJobEventRecord eventRecord, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AskJobEventRecord>> GetSinceAsync(Guid jobId, long afterEventId, CancellationToken cancellationToken = default);
    Task<AskJobEventRecord?> GetLatestAsync(Guid jobId, CancellationToken cancellationToken = default);
}
