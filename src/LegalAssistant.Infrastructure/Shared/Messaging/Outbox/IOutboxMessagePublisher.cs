using LegalAssistant.Domain.Models;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public interface IOutboxMessagePublisher
{
    string Owner { get; }

    IReadOnlyCollection<string> MessageTypes { get; }

    Task PublishAsync(
        OutboxMessageRecord message,
        CancellationToken cancellationToken = default);
}
