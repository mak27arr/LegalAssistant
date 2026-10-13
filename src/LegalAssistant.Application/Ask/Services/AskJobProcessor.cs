using System.Text.Json;
using System.Runtime.ExceptionServices;
using LegalAssistant.Application.Ask.Models;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Models;
using LegalAssistant.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Application.Ask.Services;

public sealed class AskJobProcessor : IAskJobProcessor
{
    private readonly IAskJobRepository _jobs;
    private readonly IAskJobEventRepository _events;
    private readonly IMessageOutboxWriter _outbox;
    private readonly IRagAnswerService _rag;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly AskJobProcessingOptions _options;
    private readonly ILogger<AskJobProcessor> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    public AskJobProcessor(
        IAskJobRepository jobs,
        IAskJobEventRepository events,
        IMessageOutboxWriter outbox,
        IRagAnswerService rag,
        IUnitOfWork uow,
        IClock clock,
        AskJobProcessingOptions options,
        ILogger<AskJobProcessor> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _jobs = jobs;
        _events = events;
        _outbox = outbox;
        _rag = rag;
        _uow = uow;
        _clock = clock;
        _options = options;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var lease = await _jobs.TryClaimAsync(jobId, LeaseDuration(), cancellationToken);
        if (lease is not null)
            await ProcessAsync(lease, cancellationToken);
    }

    public async Task ProcessAsync(AskJobExecutionLease lease, CancellationToken cancellationToken = default)
    {
        using var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var leaseLost = false;
        var renewalTask = RenewLeaseAsync(
            lease,
            workCancellation.Token,
            () =>
            {
                leaseLost = true;
                workCancellation.Cancel();
            });

        AskJobRecord? job = null;
        try
        {
            job = await _jobs.GetByIdAsync(lease.JobId, workCancellation.Token);
            if (job is null)
            {
                _logger.LogWarning("Claimed Ask job disappeared before processing. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
                return;
            }
            if (job.Status != AskJobStatus.InProgress || job.LeaseId != lease.LeaseId)
            {
                _logger.LogDebug("Ask job lease is no longer current; execution skipped. jobId={JobId} leaseId={LeaseId} currentLeaseId={CurrentLeaseId} status={Status}",
                    lease.JobId, lease.LeaseId, job.LeaseId, job.Status);
                return;
            }

            var startedAt = _clock.UtcNow;
            job.UpdatedAt = startedAt;
            var startedEvent = AskJobEventFactory.Create(job, AskJobStatus.InProgress, startedAt);
            await _events.AddAsync(startedEvent, workCancellation.Token);
            await _outbox.AddAsync(AskJobOutboxFactory.Create(startedEvent, startedAt), workCancellation.Token);
            await _uow.SaveChangesAsync(workCancellation.Token);

            var result = await _rag.AnswerAsync(
                new RagAnswerQuery(job.Question, job.TopK),
                workCancellation.Token);

            if (leaseLost)
                return;

            job.Status = AskJobStatus.Completed;
            job.ResultJson = JsonSerializer.Serialize(result);
            job.Error = null;
            job.LeaseExpiresAt = null;
            job.LeaseId = null;
            job.UpdatedAt = _clock.UtcNow;

            var completedEvent = AskJobEventFactory.Create(job, AskJobStatus.Completed, _clock.UtcNow, job.ResultJson);
            await _events.AddAsync(completedEvent, workCancellation.Token);
            await _outbox.AddAsync(AskJobOutboxFactory.Create(completedEvent, completedEvent.OccurredAtUtc), workCancellation.Token);
            await _uow.SaveChangesAsync(workCancellation.Token);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Ask job execution cancelled during host shutdown; releasing lease. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
            await ReleaseBestEffortAsync(lease);
            throw;
        }
        catch (OperationCanceledException ex) when (leaseLost)
        {
            _logger.LogDebug(ex, "Ask job lease was reclaimed while processing; abandoning old execution. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
        }
        catch (PersistenceConcurrencyException ex)
        {
            _logger.LogDebug(ex, "Ask job state update lost lease ownership; leaving state to the current executor. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
        }
        catch (Exception ex) when (leaseLost)
        {
            _logger.LogError(ex, "Ask job execution failed after lease was reclaimed; propagating without changing job state. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
            throw;
        }
        catch (Exception ex)
        {
            try
            {
                job ??= await _jobs.GetByIdAsync(lease.JobId, CancellationToken.None);
                if (job is null)
                {
                    _logger.LogError(ex, "Ask job disappeared while persisting its failure state. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
                    throw;
                }

                job.Status = AskJobStatus.Failed;
                job.Error = ex.Message;
                job.LeaseExpiresAt = null;
                job.LeaseId = null;
                job.UpdatedAt = _clock.UtcNow;

                var failedEvent = AskJobEventFactory.Create(job, AskJobStatus.Failed, _clock.UtcNow, null, ex.Message);
                await _events.AddAsync(failedEvent, CancellationToken.None);
                await _outbox.AddAsync(AskJobOutboxFactory.Create(failedEvent, failedEvent.OccurredAtUtc), CancellationToken.None);
                await _uow.SaveChangesAsync(CancellationToken.None);
                _logger.LogError(ex, "Ask job failed and failure state was persisted. jobId={JobId} leaseId={LeaseId} attempt={Attempt}",
                    lease.JobId, lease.LeaseId, job.AttemptCount);
            }
            catch (PersistenceConcurrencyException stateException)
            {
                _logger.LogDebug(stateException, "Ask job failure state lost lease ownership; leaving state to the current executor. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
            }
            catch (Exception stateException)
            {
                _logger.LogError(stateException,
                    "Could not persist Ask job failure state; rethrowing the original processing error. jobId={JobId} leaseId={LeaseId} attempt={Attempt} originalException={OriginalException}",
                    lease.JobId, lease.LeaseId, job?.AttemptCount, ex);
                ExceptionDispatchInfo.Capture(ex).Throw();
                throw;
            }
        }
        finally
        {
            workCancellation.Cancel();
            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogDebug(ex, "Ask job lease renewal stopped. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
            }
        }
    }

    private async Task RenewLeaseAsync(
        AskJobExecutionLease lease,
        CancellationToken cancellationToken,
        Action leaseLostCallback)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, LeaseDuration().TotalSeconds / 3));
        while (true)
        {
            await Task.Delay(interval, cancellationToken);
            using var scope = _scopeFactory?.CreateScope();
            var jobs = scope?.ServiceProvider.GetRequiredService<IAskJobRepository>() ?? _jobs;
            bool renewed;
            try
            {
                renewed = await jobs.RenewLeaseAsync(lease.JobId, lease.LeaseId, LeaseDuration(), cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Ask job lease renewal failed; will retry before expiry. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
                continue;
            }

            if (!renewed)
            {
                leaseLostCallback();
                return;
            }
        }
    }

    private async Task ReleaseBestEffortAsync(AskJobExecutionLease lease)
    {
        try
        {
            await _jobs.ReleaseInProgressAsync(lease.JobId, lease.LeaseId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release Ask job after shutdown. jobId={JobId} leaseId={LeaseId}", lease.JobId, lease.LeaseId);
        }
    }

    private TimeSpan LeaseDuration()
        => TimeSpan.FromSeconds(Math.Max(5, _options.LeaseDurationSeconds));

}
