using LegalAssistant.Api.Dtos.Jobs;
using LegalAssistant.Application.Jobs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class JobsController : ControllerBase
{
    private readonly IJobQueryService _jobs;

    public JobsController(IJobQueryService jobs)
    {
        _jobs = jobs;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetByIdAsync(id, cancellationToken);
        if (job == null) return NotFound();

        return Ok(new JobDto(
            job.Id,
            job.Type,
            job.Status,
            job.Payload,
            job.Result,
            job.LastError,
            job.CreatedAt,
            job.UpdatedAt));
    }
}
