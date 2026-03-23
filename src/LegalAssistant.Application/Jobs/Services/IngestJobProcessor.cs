using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs.Models;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Domain.Chunking;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Jobs.Services;

public sealed class IngestJobProcessor : IIngestJobProcessor
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IJobRepository _jobs;
    private readonly IUnitOfWork _uow;
    private readonly IEmbeddingEnqueueService _embeddings;
    private readonly IChunkingPolicy _chunking;
    private readonly IDocumentContentFetcher _contentFetcher;

    public IngestJobProcessor(
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        IJobRepository jobs,
        IUnitOfWork uow,
        IEmbeddingEnqueueService embeddings,
        IChunkingPolicy chunking,
        IDocumentContentFetcher contentFetcher)
    {
        _documents = documents;
        _chunks = chunks;
        _jobs = jobs;
        _uow = uow;
        _embeddings = embeddings;
        _chunking = chunking;
        _contentFetcher = contentFetcher;
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);
        if (job == null)
            throw new InvalidOperationException("Job not found");

        job.Status = JobStatus.InProgress;
        await _uow.SaveChangesAsync(cancellationToken);

        var payload = JsonSerializer.Deserialize<IngestJobPayload>(job.Payload);
        if (payload == null || string.IsNullOrWhiteSpace(payload.DocumentId))
        {
            job.Status = JobStatus.Failed;
            job.Result = "invalid payload";
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        var docId = Guid.Parse(payload.DocumentId);
        var doc = await _documents.GetByIdAsync(docId, cancellationToken);
        if (doc == null)
        {
            job.Status = JobStatus.Failed;
            job.Result = "document not found";
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(doc.Content) && !string.IsNullOrWhiteSpace(payload.Url))
        {
            var plain = await _contentFetcher.FetchPlainTextAsync(payload.Url, cancellationToken);
            if (!string.IsNullOrWhiteSpace(plain))
            {
                doc.Content = plain;
                _documents.Update(doc);
                await _uow.SaveChangesAsync(cancellationToken);
            }
        }

        var text = string.IsNullOrWhiteSpace(doc.Content) ? string.Empty : doc.Content;
        var chunkIndex = 0;

        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var range in _chunking.GetRanges(text))
            {
                var chunkText = text.Substring(range.Start, range.Length);
                var chunk = new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = doc.Id,
                    ChunkIndex = chunkIndex++,
                    Text = chunkText,
                    CharRange = $"{range.Start}-{range.EndExclusive}",
                    SourceUrl = doc.Url,
                    Embedding = null
                };

                await _chunks.AddAsync(chunk, cancellationToken);
                await _uow.SaveChangesAsync(cancellationToken);
                await _embeddings.EnqueueEmbeddingAsync(chunk.Id, chunkText, cancellationToken);
            }
        }

        job.Status = JobStatus.Completed;
        job.Result = JsonSerializer.Serialize(new { chunks = chunkIndex });
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
