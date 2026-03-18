namespace LegalAssistant.Embeddings.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "rabbitmq";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = "guest";
    public string Pass { get; init; } = "guest";
    public string QueueName { get; init; } = "embeddings:requests";
}
