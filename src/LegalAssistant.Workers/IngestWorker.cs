using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using LegalAssistant.Domain.Models;
using System.Text.RegularExpressions;
using LegalAssistant.Workers.Embeddings;
using Pgvector;
using LegalAssistant.Domain.Chunking;
using LegalAssistant.Domain.Documents;

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
                    var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

                    var job = await db.Jobs.FirstOrDefaultAsync(j => j.Status == JobStatus.Queued, stoppingToken);
                    if (job == null)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    job.Status = JobStatus.InProgress;
                    await db.SaveChangesAsync(stoppingToken);

                    var payload = JsonSerializer.Deserialize<IngestPayload>(job.Payload);
                    // Simple fetch: if Content already present, use it; otherwise attempt HTTP fetch
                    var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == Guid.Parse(payload.DocumentId), stoppingToken);
                    if (doc == null)
                    {
                        job.Status = JobStatus.Failed;
                        job.Result = "document not found";
                        await db.SaveChangesAsync(stoppingToken);
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
                                db.Documents.Update(doc);
                                await db.SaveChangesAsync(stoppingToken);
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

                            await db.DocumentChunks.AddAsync(chunk, stoppingToken);
                            await db.SaveChangesAsync(stoppingToken);
                            await _embeddingService.EnqueueEmbeddingAsync(chunk.Id, chunkText, stoppingToken);
                        }
                    }

                    job.Status = JobStatus.Completed;
                    job.Result = JsonSerializer.Serialize(new { chunks = chunkIndex });
                    await db.SaveChangesAsync(stoppingToken);

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
