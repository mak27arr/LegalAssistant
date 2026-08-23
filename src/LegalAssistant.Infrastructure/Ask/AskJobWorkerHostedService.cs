using LegalAssistant.Application.Ask;
using LegalAssistant.Infrastructure.Db;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class AskJobWorkerHostedService : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    private readonly IServiceProvider _sp;
    private readonly ILogger<AskJobWorkerHostedService> _logger;

    public AskJobWorkerHostedService(IServiceProvider sp, ILogger<AskJobWorkerHostedService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IAskJobRepository>();
                var processor = scope.ServiceProvider.GetRequiredService<IAskJobProcessor>();

                var next = await jobs.DequeueQueuedAsync(stoppingToken);
                if (next == null)
                {
                    await Task.Delay(PollDelay, stoppingToken);
                    continue;
                }

                await processor.ProcessAsync(next.Id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ask job worker cycle failed");
                await Task.Delay(PollDelay, stoppingToken);
            }
        }
    }
}
