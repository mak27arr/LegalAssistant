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
using System.Net.Http;

namespace LegalAssistant.Workers
{
    public class IngestWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<IngestWorker> _logger;

        public IngestWorker(IServiceProvider sp, ILogger<IngestWorker> logger)
        {
            _sp = sp;
            _logger = logger;
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
                    var http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();

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
                            var resp = await http.GetAsync(payload.Url, stoppingToken);
                            if (resp.IsSuccessStatusCode)
                            {
                                doc.Content = await resp.Content.ReadAsStringAsync(stoppingToken);
                                db.Documents.Update(doc);
                                await db.SaveChangesAsync(stoppingToken);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to fetch {Url} status={Status}", payload.Url, resp.StatusCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error fetching {Url}", payload.Url);
                        }
                    }

                    // rudimentary chunking: split by 2000 chars
                    var text = string.IsNullOrWhiteSpace(doc.Content) ? "" : doc.Content;
                    int chunkSize = 2000;
                    int idx = 0;
                    int chunkIndex = 0;
                    while (idx < text.Length)
                    {
                        var len = Math.Min(chunkSize, text.Length - idx);
                        var chunkText = text.Substring(idx, len);
                        var chunk = new DocumentChunk
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = doc.Id,
                            ChunkIndex = chunkIndex++,
                            Text = chunkText,
                            CharRange = $"{idx}-{idx+len}",
                            SourceUrl = doc.Url
                        };
                        await db.DocumentChunks.AddAsync(chunk, stoppingToken);
                        idx += len;
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

        private class IngestPayload { public string DocumentId { get; set; } public string Url { get; set; } }
    }
}
