namespace LegalAssistant.Messaging;

public sealed record RabbitMqMessageContext<TMessage>(
    TMessage Message,
    ReadOnlyMemory<byte> Body,
    RabbitMqMessageMetadata Metadata,
    ulong DeliveryTag,
    string QueueName,
    string Exchange,
    string RoutingKey);
