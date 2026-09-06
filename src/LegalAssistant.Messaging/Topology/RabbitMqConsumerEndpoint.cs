namespace LegalAssistant.Messaging;

public sealed class RabbitMqConsumerEndpoint
{
    public RabbitMqConsumerEndpoint(string queueName)
    {
        QueueName = queueName;
    }

    public string QueueName { get; }
    public string? ExchangeName { get; init; }
    public string ExchangeType { get; init; } = RabbitMQ.Client.ExchangeType.Direct;
    public string BindingRoutingKey { get; init; } = string.Empty;
    public bool Durable { get; init; } = true;
    public bool Exclusive { get; init; }
    public bool AutoDelete { get; init; }
    public ushort PrefetchCount { get; init; } = 1;
    public bool DeclareRetryQueue { get; init; } = true;
    public RabbitMqMalformedMessageBehavior MalformedMessageBehavior { get; init; } = RabbitMqMalformedMessageBehavior.DeadLetter;
    public RabbitMqDeadLetterDefinition? DeadLetter { get; init; }
    public IReadOnlyDictionary<string, object>? QueueArguments { get; init; }
}
