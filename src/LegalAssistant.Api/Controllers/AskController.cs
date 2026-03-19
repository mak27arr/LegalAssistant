using LegalAssistant.Application.Ask;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AskController : ControllerBase
{
    private readonly IAskService _ask;

    public AskController(IAskService ask)
    {
        _ask = ask;
    }

    [HttpPost]
    public async Task<ActionResult<AskResponse>> Ask([FromBody] AskRequest req, CancellationToken cancellationToken)
    {
        var result = await _ask.AskAsync(new AskQuery(req.Question, req.TopK ?? 5), cancellationToken);

        return Ok(new AskResponse(
            result.Question,
            result.TopK,
            result.Chunks.Select(c => new AskChunkDto(c.ChunkId, c.DocumentId, c.ChunkIndex, c.Text, c.SourceUrl, c.Score)).ToList()));
    }
}

public sealed record AskRequest(string Question, int? TopK);

public sealed record AskChunkDto(Guid ChunkId, Guid DocumentId, int ChunkIndex, string Text, string? SourceUrl, double Score);

public sealed record AskResponse(string Question, int TopK, IReadOnlyList<AskChunkDto> Chunks);
