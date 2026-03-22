using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using LegalAssistant.Domain.Models;
using System.Text.RegularExpressions;
using LegalAssistant.Workers.Embeddings;
using Pgvector;
using LegalAssistant.Domain.Chunking;
using LegalAssistant.Domain.Documents;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Common;

namespace LegalAssistant.Workers
{
    public partial class IngestWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<IngestWorker> _logger;
        private readonly IEmbeddingService _embeddingService;
        private readonly IChunkingPolicy _chunkingPolicy;
        private readonly IDocumentContentFetcher _contentFetcher;

        public IngestWorker(IServiceProvider sp, ILogger<IngestWorker> logger, IEmbeddingService embeddingService, IChunkingPolicy chunkingPolicy, IDocumentContentFetcher contentFetcher)
        {
            _sp = sp;
            _logger = logger;
            _embeddingService = embeddingService;
            _chunkingPolicy = chunkingPolicy;
            _contentFetcher = contentFetcher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("IngestWorker started"); 

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                    var chunks = scope.ServiceProvider.GetRequiredService<IDocumentChunkRepository>();
                    var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                    var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var correlation = scope.ServiceProvider.GetRequiredService<ICorrelationContext>();

                    var job = await jobQueue.DequeueQueuedAsync(stoppingToken);
                    if (job == null)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    correlation.CorrelationId = job.Id.ToString("N");
                    using var _ = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["correlationId"] = correlation.CorrelationId,
                        ["jobId"] = job.Id
                    });

                    _logger.LogInformation("Picked ingest job {JobId}", job.Id);

                    job.Status = JobStatus.InProgress;
                    await uow.SaveChangesAsync(stoppingToken);

                    var payload = JsonSerializer.Deserialize<IngestPayload>(job.Payload);
                    // Simple fetch: if Content already present, use it; otherwise attempt HTTP fetch
                    var doc = await documents.GetByIdAsync(Guid.Parse(payload.DocumentId), stoppingToken);
                    if (doc == null)
                    {
                        job.Status = JobStatus.Failed;
                        job.Result = "document not found";
                        await uow.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(doc.Content) && !string.IsNullOrWhiteSpace(payload.Url))
                    {
                        try
                        {
                            _logger.LogInformation("Fetching {Url}", payload.Url);
                            var plain = await _contentFetcher.FetchPlainTextAsync(payload.Url, stoppingToken);
                            if (!string.IsNullOrWhiteSpace(plain))
                            {
                                doc.Content = plain;
                                documents.Update(doc);
                                await uow.SaveChangesAsync(stoppingToken);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to fetch/parse content for {Url}", payload.Url);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error fetching {Url}", payload.Url);
                        }
                    }

                    var text = string.IsNullOrWhiteSpace(doc.Content) ? string.Empty : doc.Content;
                    int chunkIndex = 0;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _logger.LogInformation("Chunking document {DocumentId}. TextLength={TextLength}", doc.Id, text.Length);
                        foreach (var range in _chunkingPolicy.GetRanges(text))
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

                            await chunks.AddAsync(chunk, stoppingToken);
                            await uow.SaveChangesAsync(stoppingToken);
                            await _embeddingService.EnqueueEmbeddingAsync(chunk.Id, chunkText, stoppingToken);

                            if (chunkIndex % 25 == 0)
                            {
                                _logger.LogInformation("Enqueued embeddings for {Chunks} chunks so far", chunkIndex);
                            }
                        }
                    }

                    job.Status = JobStatus.Completed;
                    job.Result = JsonSerializer.Serialize(new { chunks = chunkIndex });
                    await uow.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Ingest job {JobId} completed, chunks={Chunks}", job.Id, chunkIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in IngestWorker");
                }
            }
        }
    }
}
