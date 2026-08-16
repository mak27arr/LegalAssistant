using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Api.Dtos.Ask;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AskController : ControllerBase
{
    private readonly IRagAnswerService _rag;

    public AskController(IRagAnswerService rag)
    {
        _rag = rag;
    }

    [HttpPost]
    public async Task<ActionResult<AskResponse>> Ask([FromBody] AskRequest req, CancellationToken cancellationToken)
    {
        var result = await _rag.AnswerAsync(new RagAnswerQuery(req.Question, req.TopK ?? 5), cancellationToken);

        return Ok(new AskResponse(
            result.Question,
            result.Answer,
            result.Sources.Select(c => new AskChunkDto(c.ChunkId, c.DocumentId, c.ChunkIndex, c.Text, c.SourceUrl, c.Score)).ToList(),
            result.IsGrounded,
            result.CitationIds,
            result.ValidationIssues));
    }

    [HttpPost("prompt")]
    public async Task<ActionResult<AskPromptResponse>> Prompt([FromBody] AskRequest req, CancellationToken cancellationToken)
    {
        var result = await _rag.BuildPromptAsync(new RagAnswerQuery(req.Question, req.TopK ?? 5), cancellationToken);
        return Ok(new AskPromptResponse(
            result.Question,
            result.TopK,
            result.Prompt,
            result.Sources
            .Select(c => new AskChunkDto(c.ChunkId, c.DocumentId, c.ChunkIndex, c.Text, c.SourceUrl, c.Score))
            .ToList()));
    }
}
