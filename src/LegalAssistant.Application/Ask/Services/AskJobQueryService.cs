using System.Text.Json;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Rag.Models;

namespace LegalAssistant.Application.Ask.Services;

public sealed class AskJobQueryService : IAskJobQueryService
{
    private readonly IAskJobRepository _jobs;

    public AskJobQueryService(IAskJobRepository jobs)
    {
        _jobs = jobs;
    }

    public async Task<AskJobDetails?> GetByIdAsync(Guid jobId, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, ownerUserId, cancellationToken);
        if (job == null)
            return null;

        RagAnswerResult? result = null;
        if (!string.IsNullOrWhiteSpace(job.ResultJson))
        {
            result = JsonSerializer.Deserialize<RagAnswerResult>(job.ResultJson);
        }

        return new AskJobDetails(
            job.Id,
            job.Status,
            job.Question,
            job.TopK,
            job.ConversationId,
            job.Error,
            result,
            job.CreatedAt,
            job.UpdatedAt);
    }
}
