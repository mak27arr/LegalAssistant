namespace LegalAssistant.Application.Ask;

public interface IAskJobProcessor
{
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task ProcessAsync(AskJobExecutionLease lease, CancellationToken cancellationToken = default);
}
