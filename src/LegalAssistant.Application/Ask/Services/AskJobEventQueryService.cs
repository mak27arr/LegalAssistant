using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Services;

public sealed class AskJobEventQueryService : IAskJobEventQueryService
{
    private readonly IAskJobEventRepository _events;

    public AskJobEventQueryService(IAskJobEventRepository events)
    {
        _events = events;
    }

    public Task<IReadOnlyList<AskJobEventRecord>> GetSinceAsync(Guid jobId, long afterEventId, CancellationToken cancellationToken = default)
        => _events.GetSinceAsync(jobId, afterEventId, cancellationToken);

    public Task<AskJobEventRecord?> GetLatestAsync(Guid jobId, CancellationToken cancellationToken = default)
        => _events.GetLatestAsync(jobId, cancellationToken);
}
