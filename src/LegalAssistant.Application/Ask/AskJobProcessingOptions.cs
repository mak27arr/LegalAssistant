namespace LegalAssistant.Application.Ask;

public sealed class AskJobProcessingOptions
{
    public int PollIntervalSeconds { get; set; } = 2;
    public int LeaseDurationSeconds { get; set; } = 120;
    public int RecoveryIntervalSeconds { get; set; } = 30;
    public int RecoveryBatchSize { get; set; } = 25;
}
