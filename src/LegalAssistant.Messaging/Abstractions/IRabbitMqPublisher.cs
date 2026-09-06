namespace LegalAssistant.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync<T>(
        RabbitMqPublishAddress address,
        T message,
        RabbitMqMessageMetadata metadata,
        CancellationToken cancellationToken = default);

    Task PublishRawAsync(
        RabbitMqPublishAddress address,
        ReadOnlyMemory<byte> body,
        RabbitMqMessageMetadata metadata,
        CancellationToken cancellationToken = default);
}
