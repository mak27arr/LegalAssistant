using LegalAssistant.Application.Ask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class AskJobRecoveryHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AskJobProcessingOptions _options;
    private readonly ILogger<AskJobRecoveryHostedService> _logger;

    public AskJobRecoveryHostedService(
        IServiceProvider serviceProvider,
        AskJobProcessingOptions options,
        ILogger<AskJobRecoveryHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IAskJobRepository>();
                var recovered = await jobs.RecoverExpiredAsync(_options.RecoveryBatchSize, stoppingToken);
                if (recovered > 0)
                    _logger.LogWarning("Recovered expired Ask jobs. count={Count}", recovered);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Ask job recovery stopped during host shutdown.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ask job recovery cycle failed");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.RecoveryIntervalSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug(ex, "Ask job recovery delay cancelled during host shutdown.");
                return;
            }
        }
    }
}
