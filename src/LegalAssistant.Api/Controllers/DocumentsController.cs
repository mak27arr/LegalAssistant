using LegalAssistant.Api.Dtos.Documents;
using LegalAssistant.Api.Mappers;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Documents.Models;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentCommandService _commands;
    private readonly IDocumentQueryService _queries;
    private readonly IDocumentStatsQueryService _stats;

    public DocumentsController(
        IDocumentCommandService commands,
        IDocumentQueryService queries,
        IDocumentStatsQueryService stats)
    {
        _commands = commands;
        _queries = queries;
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
    public async Task<ActionResult<IReadOnlyList<DocumentListItemDto>>> List(CancellationToken cancellationToken)
    {
        var documents = await _queries.GetListAsync(cancellationToken);
        return Ok(documents.Select(DocumentMapper.Map).ToList());
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
