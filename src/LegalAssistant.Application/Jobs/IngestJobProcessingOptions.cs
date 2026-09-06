namespace LegalAssistant.Application.Jobs;

public sealed class IngestJobProcessingOptions
{
    public int MaxAttempts { get; set; } = 5;
    public int InitialDelaySeconds { get; set; } = 5;
    public int MaxDelaySeconds { get; set; } = 300;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int LeaseDurationSeconds { get; set; } = 120;
    public int RecoveryIntervalSeconds { get; set; } = 30;
    public int RecoveryBatchSize { get; set; } = 25;
}
