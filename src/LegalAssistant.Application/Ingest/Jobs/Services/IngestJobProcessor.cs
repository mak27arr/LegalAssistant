using System.Collections.Generic;
using System.Text.Json;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs.Models;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Application.Jobs.Services;

public sealed class IngestJobProcessor : IIngestJobProcessor
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IJobRepository _jobs;
    private readonly IUnitOfWork _uow;
    private readonly IEmbeddingEnqueueService _embeddings;
    private readonly IEmbeddingStatusService _embeddingStatuses;
    private readonly IChunkingRunService _chunkingRunService;
    private readonly IChunkingRunRepository _chunkingRuns;
    private readonly IDocumentContentFetcher _contentFetcher;
    private readonly IClock _clock;
    private readonly IngestJobProcessingOptions _options;
    private readonly ILogger<IngestJobProcessor> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    public IngestJobProcessor(
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        IJobRepository jobs,
        IUnitOfWork uow,
        IEmbeddingEnqueueService embeddings,
        IEmbeddingStatusService embeddingStatuses,
        IChunkingRunService chunkingRunService,
        IChunkingRunRepository chunkingRuns,
        IDocumentContentFetcher contentFetcher,
        IClock clock,
        IngestJobProcessingOptions options,
        ILogger<IngestJobProcessor> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _documents = documents;
        _chunks = chunks;
        _jobs = jobs;
        _uow = uow;
        _embeddings = embeddings;
        _embeddingStatuses = embeddingStatuses;
        _chunkingRunService = chunkingRunService;
        _chunkingRuns = chunkingRuns;
        _contentFetcher = contentFetcher;
        _clock = clock;
        _options = options;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var options = _options;
        var lease = await _jobs.TryMarkInProgressAsync(
            jobId,
            TimeSpan.FromSeconds(Math.Max(1, options.LeaseDurationSeconds)),
            cancellationToken);

        if (lease is null)
            return;

        var hostCancellationToken = cancellationToken;
        using var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken);
        var leaseLost = false;
        var renewalTask = RenewLeaseAsync(
            jobId,
            lease.LeaseId,
            TimeSpan.FromSeconds(Math.Max(5, options.LeaseDurationSeconds)),
            workCancellation.Token,
            () =>
            {
                leaseLost = true;
                workCancellation.Cancel();
            });
        cancellationToken = workCancellation.Token;

        JobRecord? job = null;
        try
        {
            job = await RequireJobAsync(jobId, cancellationToken);
            var payload = RequirePayload(job.Payload);
            var doc = await RequireDocumentAsync(payload.DocumentId, cancellationToken);

            await LoadContentIfEmptyAsync(payload, doc, cancellationToken);

            var text = NormalizeText(doc.Content);
            var chunkIndex = 0;

            var existingRun = await _chunkingRuns.GetByJobIdAsync(jobId, cancellationToken);
            var (newRun, chunking) = await _chunkingRunService.CreateAsync(
                new ChunkingRunContext(doc.Id, doc.Url, text),
                cancellationToken);
            var run = existingRun ?? newRun;

            if (existingRun is null)
            {
                run.JobId = jobId;
                await _chunkingRuns.AddAsync(run, cancellationToken);
            }

            doc.ActiveChunkingRunId = run.Id;
            _documents.Update(doc);

            var toEnqueue = new List<(Guid ChunkId, string Text)>();

            if (!string.IsNullOrWhiteSpace(text))
            {
                foreach (var range in chunking.GetRanges(text))
                {
                    var chunk = await AddChunkAsync(doc, text, ++chunkIndex, run, jobId, range, cancellationToken);
                    toEnqueue.Add((chunk.Id, chunk.Text));
                }
            }

            run.TotalChunks = chunkIndex;
            run.Status = chunkIndex == 0
                ? ChunkingRunStatus.Completed
                : ChunkingRunStatus.EmbeddingInProgress;
            run.UpdatedAt = _clock.UtcNow;
            job.Result = IngestJobResultSerializer.Serialize(
                chunkIndex,
                chunkIndex == 0 ? EmbeddingStatus.Completed : EmbeddingStatus.Pending);

            foreach (var (chunkId, chunkText) in toEnqueue)
                await _embeddings.EnqueueEmbeddingAsync(chunkId, chunkText, jobId, run.Id, cancellationToken);

            // Chunks and embedding requests are committed together. The embedding
            // outbox dispatcher only publishes after this transaction succeeds.
            await _uow.SaveChangesAsync(cancellationToken);

            var finalState = await _embeddingStatuses.FinalizeRunAsync(run.Id, jobId, cancellationToken);
            if (!finalState.RunCompleted && job.Status == JobStatus.InProgress)
            {
                job.Status = JobStatus.EmbeddingInProgress;
                job.LeaseExpiresAt = null;
                job.LeaseId = null;
                job.UpdatedAt = _clock.UtcNow;
                await _uow.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (hostCancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Ingest job execution cancelled during host shutdown; releasing lease. jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId}",
                jobId, job?.CorrelationId, lease.LeaseId);
            await ReleaseInProgressBestEffortAsync(jobId, lease.LeaseId);
            throw;
        }
        catch (OperationCanceledException ex) when (leaseLost)
        {
            _logger.LogDebug(ex,
                "Ingest job lease was reclaimed while processing; abandoning old execution. jobId={JobId} leaseId={LeaseId}",
                jobId,
                lease.LeaseId);
        }
        catch (Exception ex) when (leaseLost)
        {
            _logger.LogWarning(ex,
                "Ingest processing failed after lease was reclaimed; propagating without changing job state. jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId}",
                jobId, job?.CorrelationId, lease.LeaseId);
            throw;
        }
        catch (Exception ex)
        {
            var permanent = IsPermanentFailure(ex);
            var error = DescribeException(ex);

            try
            {
                var attempt = job?.AttemptCount ?? 1;
                var result = await _jobs.RecordFailureAsync(
                    jobId,
                    lease.LeaseId,
                    error,
                    permanent,
                    Math.Max(1, options.MaxAttempts),
                    CalculateRetryDelay(attempt, options),
                    cancellationToken);

                _logger.LogError(
                    ex,
                    "Ingest job processing failed. jobId={JobId} correlationId={CorrelationId} attempt={Attempt} permanent={Permanent} outcome={Outcome}",
                    jobId,
                    job?.CorrelationId ?? jobId.ToString("N"),
                    attempt,
                    permanent,
                    result);

                if (result is JobFailureResult.Failed or JobFailureResult.Ignored)
                    return;
            }
            catch (Exception stateException)
            {
                _logger.LogError(
                    stateException,
                    "Could not persist ingest job failure state; propagating the original processing error. jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId} attempt={Attempt} originalException={OriginalException}",
                    jobId,
                    job?.CorrelationId ?? jobId.ToString("N"),
                    lease.LeaseId,
                    job?.AttemptCount ?? 1,
                    ex);
                await ReleaseInProgressBestEffortAsync(jobId, lease.LeaseId);
            }

            throw;
        }
        finally
        {
            workCancellation.Cancel();
            try
            {
                await renewalTask;
            }
            catch (OperationCanceledException ex) when (workCancellation.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Ingest job lease renewal stopped. jobId={JobId} correlationId={CorrelationId} leaseId={LeaseId}",
                    jobId, job?.CorrelationId, lease.LeaseId);
            }
        }
    }

    private async Task RenewLeaseAsync(
        Guid jobId,
        Guid leaseId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken,
        Action leaseLostCallback)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, leaseDuration.TotalSeconds / 3));
        while (true)
        {
            await Task.Delay(interval, cancellationToken);
            using var scope = _scopeFactory?.CreateScope();
            var jobs = scope?.ServiceProvider.GetRequiredService<IJobRepository>() ?? _jobs;
            bool renewed;
            try
            {
                renewed = await jobs.RenewLeaseAsync(jobId, leaseId, leaseDuration, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Ingest job lease renewal failed; will retry before expiry. jobId={JobId} leaseId={LeaseId}", jobId, leaseId);
                continue;
            }

            if (!renewed)
            {
                leaseLostCallback();
                return;
            }
        }
    }

    private async Task ReleaseInProgressBestEffortAsync(Guid jobId, Guid leaseId)
    {
        try
        {
            await _jobs.ReleaseInProgressAsync(jobId, leaseId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to release ingest job after processing failure or cancellation. jobId={JobId} leaseId={LeaseId}",
                jobId,
                leaseId);
        }
    }

    private static bool IsPermanentFailure(Exception exception)
        => exception is IngestJobPermanentException
            or ArgumentException
            or FormatException
            or JsonException;

    private static string DescribeException(Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "No error message was provided."
            : exception.Message;
        return $"{exception.GetType().Name}: {message}";
    }

    private static TimeSpan CalculateRetryDelay(int attempt, IngestJobProcessingOptions options)
    {
        var safeAttempt = Math.Max(1, attempt);
        var initial = Math.Max(0, options.InitialDelaySeconds);
        var multiplier = Math.Max(0, options.BackoffMultiplier);
        var maximum = Math.Max(0, options.MaxDelaySeconds);
        var delay = initial * Math.Pow(multiplier, safeAttempt - 1);
        return TimeSpan.FromSeconds(Math.Clamp(Math.Round(delay), 0, maximum));
    }

    private async Task<DocumentChunk> AddChunkAsync(Document doc, string text, int chunkIndex, ChunkingRun run, Guid jobId, Domain.Chunking.ChunkRange range, CancellationToken cancellationToken)
    {
        var existing = await _chunks.GetByChunkingRunAndIndexAsync(run.Id, chunkIndex, cancellationToken);
        if (existing is not null)
            return existing;

        var chunkText = NormalizeText(text.Substring(range.Start, range.Length));
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            ChunkingRunId = run.Id,
            ChunkIndex = chunkIndex,
            Text = chunkText,
            CharRange = $"{range.Start}-{range.EndExclusive}",
            SourceUrl = doc.Url,
            JobId = jobId,
            EmbeddingStatus = EmbeddingStatus.Pending,
            Embedding = null
        };

        await _chunks.AddAsync(chunk, cancellationToken);
        return chunk;
    }

    private async Task LoadContentIfEmptyAsync(IngestJobPayload payload, Document doc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doc.Content) && !string.IsNullOrWhiteSpace(payload.Url))
        {
            var plain = await _contentFetcher.FetchPlainTextAsync(payload.Url, cancellationToken);
            if (!string.IsNullOrWhiteSpace(plain))
            {
                doc.Content = NormalizeText(plain);
                _documents.Update(doc);
            }
        }
    }

    private static string NormalizeText(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\0", string.Empty);

    private async Task<JobRecord> RequireJobAsync(Guid jobId, CancellationToken cancellationToken)
        => await _jobs.GetByIdAsync(jobId, cancellationToken)
           ?? throw new IngestJobPermanentException("Job not found");

    private static IngestJobPayload RequirePayload(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<IngestJobPayload>(payloadJson);
        if (payload == null || string.IsNullOrWhiteSpace(payload.DocumentId))
            throw new IngestJobPermanentException("Invalid ingest job payload");

        return payload;
    }

    private async Task<Document> RequireDocumentAsync(string documentId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(documentId, out var docId) || docId == Guid.Empty)
            throw new IngestJobPermanentException("Invalid document id");

        return await _documents.GetByIdAsync(docId, cancellationToken)
               ?? throw new IngestJobPermanentException("Document not found");
    }
}
