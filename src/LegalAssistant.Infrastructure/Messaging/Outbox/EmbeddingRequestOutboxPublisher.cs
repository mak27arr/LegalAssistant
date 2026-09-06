using System.Text;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Messaging;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class EmbeddingRequestOutboxPublisher : IOutboxMessagePublisher
{
    private readonly IRabbitMqPublisher _publisher;

    public EmbeddingRequestOutboxPublisher(IRabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    public IReadOnlyCollection<string> MessageTypes { get; } =
        [EmbeddingRequestMessageNames.MessageType];

    public Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken = default)
        => _publisher.PublishRawAsync(
            new RabbitMqPublishAddress(string.Empty, message.RoutingKey),
            Encoding.UTF8.GetBytes(message.Payload),
            new RabbitMqMessageMetadata
            {
                MessageId = message.DeduplicationKey ?? message.Id.ToString("N"),
                CorrelationId = message.CorrelationId,
                MessageType = message.MessageType
            },
            cancellationToken);
}
