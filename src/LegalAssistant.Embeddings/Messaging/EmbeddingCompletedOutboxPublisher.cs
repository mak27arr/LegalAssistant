using System.Text;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Messaging.Outbox;
using LegalAssistant.Messaging;

namespace LegalAssistant.Embeddings.Messaging;

public sealed class EmbeddingCompletedOutboxPublisher : IOutboxMessagePublisher
{
    private readonly IRabbitMqPublisher _publisher;

    public EmbeddingCompletedOutboxPublisher(IRabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    public string Owner => OutboxMessageOwners.Embeddings;

    public IReadOnlyCollection<string> MessageTypes { get; } =
        [EmbeddingCompletedMessageNames.MessageType];

    public Task PublishAsync(
        OutboxMessageRecord message,
        CancellationToken cancellationToken = default)
        => _publisher.PublishRawAsync(
            new RabbitMqPublishAddress(string.Empty, EmbeddingCompletedMessageNames.Queue),
            Encoding.UTF8.GetBytes(message.Payload),
            new RabbitMqMessageMetadata
            {
                MessageId = message.DeduplicationKey ?? message.Id.ToString("N"),
                CorrelationId = message.CorrelationId,
                MessageType = message.MessageType
            },
            cancellationToken);
}
