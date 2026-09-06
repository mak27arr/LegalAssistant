using LegalAssistant.Api.Dtos.Chunks;
using LegalAssistant.Api.Mappers;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Embeddings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ChunksController : ControllerBase
{
    private readonly IDocumentChunkQueryService _chunks;
    private readonly IEmbeddingReplayService _embeddingReplay;

    public ChunksController(
        IDocumentChunkQueryService chunks,
        IEmbeddingReplayService embeddingReplay)
    {
        _chunks = chunks;
        _embeddingReplay = embeddingReplay;
    }

    [HttpGet("{chunkId:guid}")]
    public async Task<ActionResult<ChunkDetailsDto>> Get(Guid chunkId, CancellationToken cancellationToken)
    {
        var chunk = await _chunks.GetByIdAsync(chunkId, cancellationToken);
        if (chunk == null)
            return NotFound();

        return Ok(ChunkMapper.Map(chunk));
    }

    [HttpPost("{chunkId:guid}/embedding/replay")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReplayEmbedding(Guid chunkId, CancellationToken cancellationToken)
    {
        var replayed = await _embeddingReplay.ReplayAsync(chunkId, cancellationToken);
        if (!replayed)
            return NotFound();

        return Accepted(new { chunkId, status = "Pending" });
    }
}
