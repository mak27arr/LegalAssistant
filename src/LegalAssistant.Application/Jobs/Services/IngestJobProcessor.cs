using System.Collections.Generic;
using System.Text.Json;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Chunking.Models;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs.Models;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Jobs.Services;

public sealed class IngestJobProcessor : IIngestJobProcessor
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IJobRepository _jobs;
    private readonly IUnitOfWork _uow;
    private readonly IEmbeddingEnqueueService _embeddings;
    private readonly IChunkingRunService _chunkingRunService;
    private readonly IChunkingRunRepository _chunkingRuns;
    private readonly IDocumentContentFetcher _contentFetcher;

    public IngestJobProcessor(
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        IJobRepository jobs,
        IUnitOfWork uow,
        IEmbeddingEnqueueService embeddings,
        IChunkingRunService chunkingRunService,
        IChunkingRunRepository chunkingRuns,
        IDocumentContentFetcher contentFetcher)
    {
        _documents = documents;
        _chunks = chunks;
        _jobs = jobs;
        _uow = uow;
        _embeddings = embeddings;
        _chunkingRunService = chunkingRunService;
        _chunkingRuns = chunkingRuns;
        _contentFetcher = contentFetcher;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await RequireJobAsync(jobId, cancellationToken);

        try
        {
            job.Status = JobStatus.InProgress;
            await _uow.SaveChangesAsync(cancellationToken);

            var payload = RequirePayload(job.Payload);
            var doc = await RequireDocumentAsync(payload.DocumentId, cancellationToken);

            await LoadContentIfEmptyAsync(payload, doc, cancellationToken);

            var text = string.IsNullOrWhiteSpace(doc.Content) ? string.Empty : doc.Content;
            var chunkIndex = 0;

            var (run, chunking) = await _chunkingRunService.CreateAsync(
                new ChunkingRunContext(doc.Id, doc.Url, text),
                cancellationToken);

            await _chunkingRuns.AddAsync(run, cancellationToken);

            doc.ActiveChunkingRunId = run.Id;
            _documents.Update(doc);

            var toEnqueue = new List<(Guid ChunkId, string Text)>();

            if (!string.IsNullOrWhiteSpace(text))
            {
                foreach (var range in chunking.GetRanges(text))
                {
                    var chunk = await AddChunkAsync(doc, text, ++chunkIndex, run, range, cancellationToken);
                    toEnqueue.Add((chunk.Id, chunk.Text));
                }
            }

            job.Status = JobStatus.Completed;
            job.Result = JsonSerializer.Serialize(new { chunks = chunkIndex });

            await _uow.SaveChangesAsync(cancellationToken);

            foreach (var (chunkId, chunkText) in toEnqueue)
                await _embeddings.EnqueueEmbeddingAsync(chunkId, chunkText, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
        {
            job.Status = JobStatus.Failed;
            job.Result = ex.Message;
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<DocumentChunk> AddChunkAsync(Document doc, string text, int chunkIndex, ChunkingRun run, Domain.Chunking.ChunkRange range, CancellationToken cancellationToken)
    {
        var chunkText = text.Substring(range.Start, range.Length);
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            ChunkingRunId = run.Id,
            ChunkIndex = chunkIndex,
            Text = chunkText,
            CharRange = $"{range.Start}-{range.EndExclusive}",
            SourceUrl = doc.Url,
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
                doc.Content = plain;
                _documents.Update(doc);
            }
        }
    }

    private async Task<JobRecord> RequireJobAsync(Guid jobId, CancellationToken cancellationToken)
        => await _jobs.GetByIdAsync(jobId, cancellationToken)
           ?? throw new InvalidOperationException("Job not found");

    private static IngestJobPayload RequirePayload(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<IngestJobPayload>(payloadJson);
        if (payload == null || string.IsNullOrWhiteSpace(payload.DocumentId))
            throw new ArgumentException("invalid payload");

        return payload;
    }

    private async Task<Document> RequireDocumentAsync(string documentId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(documentId, out var docId) || docId == Guid.Empty)
            throw new FormatException("invalid document id");

        return await _documents.GetByIdAsync(docId, cancellationToken)
               ?? throw new InvalidOperationException("document not found");
    }
}
