using LegalAssistant.Api.Dtos.Jobs;
using LegalAssistant.Infrastructure.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class JobsController : ControllerBase
{
    private readonly LegalAssistantDbContext _db;

    public JobsController(LegalAssistantDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobDto>> Get(Guid id)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job == null) return NotFound();

        return Ok(new JobDto(job.Id, job.Type.ToString(), job.Status.ToString(), job.Payload, job.Result, job.CreatedAt, job.UpdatedAt));
    }
}
