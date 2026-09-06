using LegalAssistant.Domain.Models;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public interface IOutboxMessagePublisher
{
    IReadOnlyCollection<string> MessageTypes { get; }

    Task PublishAsync(
        OutboxMessageRecord message,
        CancellationToken cancellationToken = default);
}
