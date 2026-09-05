using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Ask.Services;

public interface IAskJobEventStreamUseCase
{
    IAsyncEnumerable<AskJobStreamItem> StreamEventsAsync(
        Guid jobId,
        Guid ownerUserId,
        string? sessionId,
        long lastEventId,
        CancellationToken cancellationToken = default);
}
