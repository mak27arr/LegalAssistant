namespace LegalAssistant.Messaging;

public sealed record RabbitMqDeadLetterDefinition(
    string ExchangeName,
    string QueueName,
    string RoutingKey);
