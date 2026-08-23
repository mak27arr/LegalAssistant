using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobEventFanout
{
    IAskJobEventSubscription Subscribe(Guid jobId);
    Task PublishAsync(AskJobEventRecord eventRecord, CancellationToken cancellationToken = default);
}
