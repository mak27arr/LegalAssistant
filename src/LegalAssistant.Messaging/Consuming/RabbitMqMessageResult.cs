namespace LegalAssistant.Messaging;

public enum RabbitMqMessageResult
{
    Ack,
    Retry,
    DeadLetter
}
