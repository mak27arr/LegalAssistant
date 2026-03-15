using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using LegalAssistant.Api.Services;

namespace LegalAssistant.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentsController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDocumentRequest req)
        {
            var jobId = await _documentService.CreateDocumentAsync(req.Title, req.Url, req.Content, req.Metadata);
            return Accepted(new { jobId });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var doc = await _documentService.GetDocumentAsync(id);
            if (doc == null) return NotFound();
            return Ok(doc);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentRequest req)
        {
            var ok = await _documentService.UpdateDocumentAsync(id, req.Title, req.Content, req.Metadata);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var ok = await _documentService.DeleteDocumentAsync(id);
            if (!ok) return NotFound();
            return NoContent();
        }
    }

    public class CreateDocumentRequest
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public object Metadata { get; set; }
    }

    public class UpdateDocumentRequest
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public object Metadata { get; set; }
    }
}
