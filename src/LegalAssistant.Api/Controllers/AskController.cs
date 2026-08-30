using LegalAssistant.Api.Dtos.Ask;
using LegalAssistant.Api.Filters;
using LegalAssistant.Api.Mappers;
using LegalAssistant.Api.Services;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AskController : ControllerBase
{
    private readonly IRagAnswerService _rag;
    private readonly IAskJobService _askJobs;
    private readonly IAskJobQueryService _askJobQueries;
    private readonly IAskJobEventStreamService _askJobEvents;

    public AskController(IRagAnswerService rag, IAskJobService askJobs, IAskJobQueryService askJobQueries, IAskJobEventStreamService askJobEvents)
    {
        _rag = rag;
        _askJobs = askJobs;
        _askJobQueries = askJobQueries;
        _askJobEvents = askJobEvents;
    }

    [HttpPost]
    public async Task<ActionResult<AskResponse>> Ask([FromBody] AskRequest req, CancellationToken cancellationToken)
    {
        var result = await _rag.AnswerAsync(new RagAnswerQuery(req.Question, req.TopK ?? 5), cancellationToken);

        return Ok(new AskResponse(
            result.Question,
            result.Answer,
            result.IsGrounded));
    }

    [HttpPost("async")]
    [RequireIdempotencyKey]
    public async Task<ActionResult<AskJobSubmissionResponse>> AskAsync(
        [FromBody] AskAsyncRequest req,
        [FromHeader(Name = "X-Actor-Key")] string? actorKey,
        CancellationToken cancellationToken)
    {
        var submission = await _askJobs.SubmitAsync(
            new AskJobSubmissionCommand(
                req.Question,
                req.TopK ?? 5,
                req.ConversationId,
                string.IsNullOrWhiteSpace(actorKey) ? "anonymous" : actorKey.Trim(),
                HttpContext.GetRequiredIdempotencyKey()),
            cancellationToken);

        return Accepted(new AskJobSubmissionResponse(
            submission.JobId,
            submission.Status.ToString(),
            submission.IsNew,
            submission.ActorScopeKey,
            submission.IdempotencyKey,
            submission.CreatedAt,
            submission.UpdatedAt));
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<ActionResult<AskJobResponse>> GetJob(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _askJobQueries.GetByIdAsync(jobId, cancellationToken);
        if (job == null)
            return NotFound();

        return Ok(AskResponseMapper.Map(job));
    }

    [HttpGet("jobs/{jobId:guid}/events")]
    [AllowAnonymous]
    [Produces("text/event-stream")]
    public Task Events(Guid jobId, CancellationToken cancellationToken)
        => _askJobEvents.StreamAsync(jobId, HttpContext, cancellationToken);

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
