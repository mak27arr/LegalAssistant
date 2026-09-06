namespace LegalAssistant.Messaging;

public sealed record RabbitMqPublishAddress(string Exchange, string RoutingKey, bool Mandatory = false);
