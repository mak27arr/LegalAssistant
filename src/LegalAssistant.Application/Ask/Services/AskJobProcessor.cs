using System.Text.Json;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Services;

public sealed class AskJobProcessor : IAskJobProcessor
{
    private readonly IAskJobRepository _jobs;
    private readonly IAskJobEventRepository _events;
    private readonly IMessageOutboxWriter _outbox;
    private readonly IRagAnswerService _rag;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public AskJobProcessor(
        IAskJobRepository jobs,
        IAskJobEventRepository events,
        IMessageOutboxWriter outbox,
        IRagAnswerService rag,
        IUnitOfWork uow,
        IClock clock)
    {
        _jobs = jobs;
        _events = events;
        _outbox = outbox;
        _rag = rag;
        _uow = uow;
        _clock = clock;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!await _jobs.TryMarkInProgressAsync(jobId, cancellationToken))
            return;

        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);
        if (job == null)
            return;

        var startedAt = _clock.UtcNow;
        var startedEvent = AskJobEventFactory.Create(job, AskJobStatus.InProgress, startedAt);
        await _events.AddAsync(startedEvent, cancellationToken);
        await _outbox.AddAsync(AskJobOutboxFactory.Create(startedEvent, startedAt), cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _rag.AnswerAsync(new RagAnswerQuery(job.Question, job.TopK), cancellationToken);

            job.Status = AskJobStatus.Completed;
            job.ResultJson = JsonSerializer.Serialize(result);
            job.Error = null;
            job.UpdatedAt = _clock.UtcNow;

            var completedEvent = AskJobEventFactory.Create(job, AskJobStatus.Completed, _clock.UtcNow, job.ResultJson);
            await _events.AddAsync(completedEvent, cancellationToken);
            await _outbox.AddAsync(AskJobOutboxFactory.Create(completedEvent, completedEvent.OccurredAtUtc), cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            job.Status = AskJobStatus.Failed;
            job.Error = ex.Message;
            job.UpdatedAt = _clock.UtcNow;

            var failedEvent = AskJobEventFactory.Create(job, AskJobStatus.Failed, _clock.UtcNow, null, ex.Message);
            await _events.AddAsync(failedEvent, cancellationToken);
            await _outbox.AddAsync(AskJobOutboxFactory.Create(failedEvent, failedEvent.OccurredAtUtc), cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);
        }
    }
}
