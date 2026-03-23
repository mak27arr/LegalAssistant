using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Jobs.Services;

namespace LegalAssistant.Workers
{
    public partial class IngestWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<IngestWorker> _logger;
        private readonly IIngestJobProcessor _processor;

        public IngestWorker(IServiceProvider sp, ILogger<IngestWorker> logger, IIngestJobProcessor processor)
        {
            _sp = sp;
            _logger = logger;
            _processor = processor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("IngestWorker started"); 

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
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

                    await _processor.ProcessAsync(job.Id, stoppingToken);
                    _logger.LogInformation("Ingest job {JobId} processed", job.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in IngestWorker");
                }
            }
        }
    }
}
