namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqProcessingOptions
{
    public int MaxAttempts { get; set; } = 5;
    public int InitialDelaySeconds { get; set; } = 5;
    public int MaxDelaySeconds { get; set; } = 300;
    public double BackoffMultiplier { get; set; } = 2.0;
}
