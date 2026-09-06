namespace LegalAssistant.Messaging;

public enum RabbitMqMalformedMessageBehavior
{
    Ack,
    Retry,
    DeadLetter
}
