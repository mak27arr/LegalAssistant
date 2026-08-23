using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobEventPublisher
{
    Task PublishAsync(AskJobEventRecord eventRecord, CancellationToken cancellationToken = default);
}
