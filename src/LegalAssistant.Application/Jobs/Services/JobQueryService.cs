using System;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Jobs.Models;

namespace LegalAssistant.Application.Jobs.Services;

public sealed class JobQueryService : IJobQueryService
{
    private readonly IJobRepository _jobs;

    public JobQueryService(IJobRepository jobs)
    {
        _jobs = jobs;
    }

    public async Task<JobDto?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);
        if (job == null) return null;

        return new JobDto(job.Id, job.Type.ToString(), job.Status.ToString(), job.Payload, job.Result, job.CreatedAt, job.UpdatedAt);
    }
}
