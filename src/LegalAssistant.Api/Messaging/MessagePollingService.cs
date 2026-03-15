using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LegalAssistant.Api.Messaging;
using System.Text.Json;
using System.Linq;
using System;
using LegalAssistant.Infrastructure.Db;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Api.Messaging
{
    // Polls the InMemoryMessagePublisher queue and persists messages to Jobs table for workers to pick up
    public class MessagePollingService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<MessagePollingService> _logger;

        public MessagePollingService(IServiceProvider sp, ILogger<MessagePollingService> logger)
        {
            _sp = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MessagePollingService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (InMemoryMessagePublisher.Queue.TryDequeue(out var item))
                    {
                        using var scope = _sp.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

                        // find job by payload key if exists
                        var jobId = Guid.TryParse(item.key, out var gid) ? gid : Guid.Empty;
                        if (jobId == Guid.Empty)
                        {
                            _logger.LogWarning("Message with non-guid key received");
                        }
                        else
                        {
                            var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, stoppingToken);
                            if (job != null)
                            {
                                job.Payload = item.payload;
                                job.UpdatedAt = DateTime.UtcNow;
                                await db.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("Updated job {JobId} payload", job.Id);
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(500, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling messages");
                }
            }
        }
    }
}
