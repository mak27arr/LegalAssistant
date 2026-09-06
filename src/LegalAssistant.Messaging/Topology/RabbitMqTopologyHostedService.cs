using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Messaging;

public sealed class RabbitMqTopologyHostedService : BackgroundService
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IEnumerable<IRabbitMqTopologyDefinition> _definitions;
    private readonly IOptions<RabbitMqConnectionOptions> _connectionOptions;
    private readonly ILogger<RabbitMqTopologyHostedService> _logger;

    public RabbitMqTopologyHostedService(
        IRabbitMqConnectionProvider connectionProvider,
        IEnumerable<IRabbitMqTopologyDefinition> definitions,
        IOptions<RabbitMqConnectionOptions> connectionOptions,
        ILogger<RabbitMqTopologyHostedService> logger)
    {
        _connectionProvider = connectionProvider;
        _definitions = definitions;
        _connectionOptions = connectionOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = _connectionProvider.GetConnection(stoppingToken);
                using var channel = connection.CreateModel();
                var topology = new RabbitMqTopologyBuilder(channel);

                foreach (var definition in _definitions)
                    definition.Declare(topology);

                _logger.LogInformation("RabbitMQ topology declarations completed");
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "RabbitMQ topology declaration failed; retrying in {DelaySeconds} seconds",
                    _connectionOptions.Value.ReconnectDelay.TotalSeconds);

                try
                {
                    await Task.Delay(_connectionOptions.Value.ReconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
