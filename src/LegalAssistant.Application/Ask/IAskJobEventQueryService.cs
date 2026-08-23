using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobEventQueryService
{
    Task<IReadOnlyList<AskJobEventRecord>> GetSinceAsync(Guid jobId, long afterEventId, CancellationToken cancellationToken = default);
    Task<AskJobEventRecord?> GetLatestAsync(Guid jobId, CancellationToken cancellationToken = default);
}
