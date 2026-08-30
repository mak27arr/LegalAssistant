using LegalAssistant.Api.Dtos.Chunks;
using LegalAssistant.Api.Mappers;
using LegalAssistant.Application.Chunks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ChunksController : ControllerBase
{
    private readonly IDocumentChunkQueryService _chunks;

    public ChunksController(IDocumentChunkQueryService chunks)
    {
        _chunks = chunks;
    }

    [HttpGet("{chunkId:guid}")]
    public async Task<ActionResult<ChunkDetailsDto>> Get(Guid chunkId, CancellationToken cancellationToken)
    {
        var chunk = await _chunks.GetByIdAsync(chunkId, cancellationToken);
        if (chunk == null)
            return NotFound();

        return Ok(ChunkMapper.Map(chunk));
    }
}
