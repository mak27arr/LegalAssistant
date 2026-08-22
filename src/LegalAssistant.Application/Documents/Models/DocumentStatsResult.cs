namespace LegalAssistant.Application.Documents.Models;

public sealed record DocumentStatsResult(
    int TotalDocuments,
    int QueuedJobs,
    int InProgressJobs,
    int CompletedJobs,
    int FailedJobs);
