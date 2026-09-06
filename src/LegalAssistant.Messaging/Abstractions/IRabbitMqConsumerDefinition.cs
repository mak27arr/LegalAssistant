namespace LegalAssistant.Messaging;

public interface IRabbitMqConsumerDefinition<TMessage>
{
    RabbitMqConsumerEndpoint Endpoint { get; }

    TMessage Deserialize(ReadOnlyMemory<byte> body);

    Task<RabbitMqMessageResult> HandleAsync(
        IServiceProvider scopedServices,
        RabbitMqMessageContext<TMessage> context,
        CancellationToken cancellationToken);
}
