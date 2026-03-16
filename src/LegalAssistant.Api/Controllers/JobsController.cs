using System;
using System.Threading.Tasks;
using LegalAssistant.Infrastructure.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly LegalAssistantDbContext _db;

        public JobsController(LegalAssistantDbContext db)
        {
            _db = db;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();

            return Ok(new
            {
                job.Id,
                job.Type,
                job.Status,
                job.Payload,
                job.Result,
                job.CreatedAt,
                job.UpdatedAt
            });
        }
    }
}
