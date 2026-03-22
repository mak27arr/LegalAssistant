using LegalAssistant.Api.Dtos.Documents;
using LegalAssistant.Api.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest req, CancellationToken cancellationToken)
    {
        var jobId = await _documentService.CreateDocumentAsync(req.Title, req.Url, req.Content, req.Metadata, cancellationToken);
        return Accepted(new { jobId });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var doc = await _documentService.GetDocumentAsync(id, cancellationToken);
        if (doc == null) return NotFound();
        return Ok(doc);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest req, CancellationToken cancellationToken)
    {
        var ok = await _documentService.UpdateDocumentAsync(id, req.Title, req.Content, req.Metadata, cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _documentService.DeleteDocumentAsync(id, cancellationToken);
        if (!ok) return NotFound();
        return NoContent();
    }
}
