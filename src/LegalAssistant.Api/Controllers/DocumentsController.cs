using LegalAssistant.Api.Dtos.Documents;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Documents.Models;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentCommandService _commands;
    private readonly LegalAssistant.Application.Documents.IDocumentRepository _documents;

    public DocumentsController(IDocumentCommandService commands, LegalAssistant.Application.Documents.IDocumentRepository documents)
    {
        _commands = commands;
        _documents = documents;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest req, CancellationToken cancellationToken)
    {
        var result = await _commands.CreateAsync(new CreateDocumentCommand(req.Title, req.Url, req.Content, req.Metadata), cancellationToken);
        return Accepted(new { jobId = result.JobId, documentId = result.DocumentId });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _documents.GetByIdWithChunksAsync(id, cancellationToken);
        if (doc == null) return NotFound();
        return Ok(new DocumentDto(doc.Id, doc.Title, doc.Url, doc.Content, doc.Metadata, doc.Version));
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
