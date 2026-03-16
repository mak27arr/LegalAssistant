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
using System.Text.RegularExpressions;
using LegalAssistant.Workers.Embeddings;

namespace LegalAssistant.Workers
{
    public class IngestWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<IngestWorker> _logger;
        private readonly IEmbeddingService _embeddingService;

        public IngestWorker(IServiceProvider sp, ILogger<IngestWorker> logger, IEmbeddingService embeddingService)
        {
            _sp = sp;
            _logger = logger;
            _embeddingService = embeddingService;
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
                                var html = await resp.Content.ReadAsStringAsync(stoppingToken);
                                var plain = StripHtml(html);
                                doc.Content = plain;
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

                    // chunking by articles ("Стаття {номер}") with fallback to fixed-size chunks for long articles
                    var text = string.IsNullOrWhiteSpace(doc.Content) ? string.Empty : doc.Content;
                    int chunkIndex = 0;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // Regex для заголовків статей, наприклад: "Стаття 1.", "Стаття 1-1", "Стаття 15¹" тощо
                        var articleRegex = new Regex(@"Стаття\s+\d+[\d¹²³]*[\w\-]*", RegexOptions.Multiline | RegexOptions.CultureInvariant);
                        var matches = articleRegex.Matches(text);

                        if (matches.Count == 0)
                        {
                            // Якщо статті не знайдені, fallback до старої поведінки по 2000 символів
                            int fallbackSize = 2000;
                            int idx = 0;
                            while (idx < text.Length)
                            {
                                var len = Math.Min(fallbackSize, text.Length - idx);
                                var chunkText = text.Substring(idx, len);
                                var embedding = await _embeddingService.GetEmbeddingAsync(chunkText, stoppingToken);
                                var chunk = new DocumentChunk
                                {
                                    Id = Guid.NewGuid(),
                                    DocumentId = doc.Id,
                                    ChunkIndex = chunkIndex++,
                                    Text = chunkText,
                                    CharRange = $"{idx}-{idx + len}",
                                    SourceUrl = doc.Url,
                                    Embedding = new Pgvector.Vector(embedding)
                                };
                                await db.DocumentChunks.AddAsync(chunk, stoppingToken);
                                idx += len;
                            }
                        }
                        else
                        {
                            // Ріжемо по статтях
                            for (int i = 0; i < matches.Count; i++)
                            {
                                int start = matches[i].Index;
                                int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
                                int length = end - start;

                                if (length <= 0)
                                {
                                    continue;
                                }

                                var articleText = text.Substring(start, length);

                                // Якщо стаття надто велика, ріжемо всередині ще по 2000 символів
                                int maxChunkSize = 2000;
                                int localIdx = 0;
                                while (localIdx < articleText.Length)
                                {
                                    var len = Math.Min(maxChunkSize, articleText.Length - localIdx);
                                    var chunkText = articleText.Substring(localIdx, len);

                                    var globalStart = start + localIdx;
                                    var embedding = await _embeddingService.GetEmbeddingAsync(chunkText, stoppingToken);
                                    var chunk = new DocumentChunk
                                    {
                                        Id = Guid.NewGuid(),
                                        DocumentId = doc.Id,
                                        ChunkIndex = chunkIndex++,
                                        Text = chunkText,
                                        CharRange = $"{globalStart}-{globalStart + len}",
                                        SourceUrl = doc.Url,
                                        Embedding = new Pgvector.Vector(embedding)
                                    };
                                    await db.DocumentChunks.AddAsync(chunk, stoppingToken);

                                    localIdx += len;
                                }
                            }
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

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            // прибрати <script> і <style>
            html = Regex.Replace(html, "<(script|style)[^>]*?>.*?</\\1>", string.Empty,
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // прибрати всі теги
            html = Regex.Replace(html, "<.*?>", string.Empty);

            // decode HTML entities (&nbsp; &amp; ...)
            html = System.Net.WebUtility.HtmlDecode(html);

            // нормалізувати пробіли
            return Regex.Replace(html, "\\s+", " ").Trim();
        }

        private class IngestPayload { public string DocumentId { get; set; } public string Url { get; set; } }
    }
}
