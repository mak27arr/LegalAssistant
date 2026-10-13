namespace LegalAssistant.Application.Jobs;

public sealed record JobExecutionLease(Guid LeaseId);

public enum JobFailureResult
{
    Ignored,
    Retrying,
    Failed
}
