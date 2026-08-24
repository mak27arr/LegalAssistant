using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Logging.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqIngestConsumerHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<RabbitMqIngestConsumerHostedService> _logger;
    private readonly IOptions<RabbitMqProcessingOptions> _processingOptions;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqIngestConsumerHostedService(
        IServiceProvider sp,
        ILogger<RabbitMqIngestConsumerHostedService> logger,
        IOptions<RabbitMqProcessingOptions> processingOptions)
    {
        _sp = sp;
        _logger = logger;
        _processingOptions = processingOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var p) ? p : 5672;
        var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = pass,
            AutomaticRecoveryEnabled = true,
            DispatchConsumersAsync = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection?.Dispose();
                _connection = factory.CreateConnection();
                _channel?.Dispose();
                _channel = _connection.CreateModel();

                IngestRabbitMqTopology.EnsureAll(_connection);
                _channel.BasicQos(0, 1, false);

                _logger.LogInformation("RabbitMQ ingest consumer connected to {Host}:{Port}", host, port);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += async (_, ea) =>
                {
                    var corr = ea.BasicProperties?.CorrelationId ?? ea.BasicProperties?.MessageId;
                    _logger.LogInformation("Received ingest message, corrId={Corr}", corr);

                    try
                    {
                        using var scope = _sp.CreateScope();
                        using var correlationScope = CorrelationLogScopeFactory.Create(
                            scope.ServiceProvider,
                            _logger,
                            corr,
                            nameof(RabbitMqIngestConsumerHostedService));

                        var processor = scope.ServiceProvider.GetRequiredService<IIngestJobProcessor>();

                        if (Guid.TryParse(corr, out var jobId))
                        {
                            await processor.ProcessAsync(jobId, stoppingToken);
                        }

                        _channel!.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing ingest message");
                        _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                    }
                };

                _channel.BasicConsume(queue: IngestRabbitMqTopology.Queue, autoAck: false, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: consumer);

                var tcs = new TaskCompletionSource();
                using var reg = stoppingToken.Register(() => tcs.TrySetResult());
                await tcs.Task;
                break;
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException || ex is System.Net.Sockets.SocketException)
            {
                _logger.LogWarning("RabbitMQ is not reachable in RabbitMqIngestConsumerHostedService. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
