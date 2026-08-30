using LegalAssistant.Api.Dtos.Documents;
using LegalAssistant.Api.Dtos.Chunks;
using LegalAssistant.Api.Mappers;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Documents.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentCommandService _commands;
    private readonly IDocumentQueryService _queries;
    private readonly IDocumentChunkQueryService _chunks;
    private readonly IDocumentStatsQueryService _stats;

    public DocumentsController(
        IDocumentCommandService commands,
        IDocumentQueryService queries,
        IDocumentChunkQueryService chunks,
        IDocumentStatsQueryService stats)
    {
        _commands = commands;
        _queries = queries;
        _chunks = chunks;
        _stats = stats;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest req, CancellationToken cancellationToken)
    {
        var result = await _commands.CreateAsync(new CreateDocumentCommand(req.Title, req.Url, req.Content, req.Metadata), cancellationToken);
        return Accepted(new { jobId = result.JobId, documentId = result.DocumentId });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDetailsDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _queries.GetByIdAsync(id, cancellationToken);
        if (doc == null) return NotFound();
        return Ok(DocumentMapper.Map(doc));
    }

    [HttpGet]
    public async Task<ActionResult<DocumentListPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var documents = await _queries.GetListAsync(page, pageSize, cancellationToken);
        return Ok(DocumentMapper.Map(documents));
    }

    [HttpGet("{id}/chunks")]
    public async Task<ActionResult<ChunkPageResponse>> GetChunks(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _chunks.GetByDocumentIdAsync(id, page, pageSize, cancellationToken);
        if (result == null)
            return NotFound();

        return Ok(ChunkMapper.Map(result));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DocumentStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        var stats = await _stats.GetStatsAsync(cancellationToken);
        return Ok(new DocumentStatsDto(
            stats.TotalDocuments,
            stats.QueuedJobs,
            stats.InProgressJobs,
            stats.CompletedJobs,
            stats.FailedJobs));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest req, CancellationToken cancellationToken)
    {
        var ok = await _commands.UpdateAsync(new UpdateDocumentCommand(id, req.Title, req.Content, req.Metadata), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _commands.DeleteAsync(new DeleteDocumentCommand(id), cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
