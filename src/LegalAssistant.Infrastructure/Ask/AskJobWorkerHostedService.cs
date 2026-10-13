using LegalAssistant.Application.Ask;
using LegalAssistant.Infrastructure.Db;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class AskJobWorkerHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<AskJobWorkerHostedService> _logger;
    private readonly AskJobProcessingOptions _options;

    public AskJobWorkerHostedService(
        IServiceProvider sp,
        ILogger<AskJobWorkerHostedService> logger,
        AskJobProcessingOptions options)
    {
        _sp = sp;
        _logger = logger;
        _options = options;
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

                var lease = await jobs.TryClaimNextAsync(
                    TimeSpan.FromSeconds(Math.Max(5, _options.LeaseDurationSeconds)),
                    stoppingToken);
                if (lease is null)
                {
                    await Task.Delay(PollDelay(), stoppingToken);
                    continue;
                }

                await processor.ProcessAsync(lease, stoppingToken);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Ask job worker stopped during host shutdown.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ask job worker cycle failed");
                await Task.Delay(PollDelay(), stoppingToken);
            }
        }
    }

    private TimeSpan PollDelay()
        => TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
}
