using System.Text;
using System.Text.Json;
using LegalAssistant.Application.Ask;
using LegalAssistant.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class RabbitMqAskJobEventRelayHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<RabbitMqAskJobEventRelayHostedService> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqAskJobEventRelayHostedService(IServiceProvider sp, ILogger<RabbitMqAskJobEventRelayHostedService> logger)
    {
        _sp = sp;
        _logger = logger;
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

                AskJobRabbitMqTopology.EnsureExchange(_connection);
                _channel.BasicQos(0, 50, false);

                var queueName = _channel.QueueDeclare(queue: "", durable: false, exclusive: true, autoDelete: true, arguments: null).QueueName;
                _channel.QueueBind(queue: queueName, exchange: AskJobRabbitMqTopology.Exchange, routingKey: "ask.job.*");

                _logger.LogInformation("RabbitMQ ask event relay connected to {Host}:{Port} queue={Queue}", host, port, queueName);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += async (_, ea) =>
                {
                    try
                    {
                        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                        var eventRecord = JsonSerializer.Deserialize<AskJobEventRecord>(payload);
                        if (eventRecord != null)
                        {
                            using var scope = _sp.CreateScope();
                            var fanout = scope.ServiceProvider.GetRequiredService<IAskJobEventFanout>();
                            await fanout.PublishAsync(eventRecord, stoppingToken);
                        }

                        _channel!.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error relaying ask event");
                        _channel!.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                };

                _channel.BasicConsume(queue: queueName, autoAck: false, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: consumer);

                var tcs = new TaskCompletionSource();
                using var reg = stoppingToken.Register(() => tcs.TrySetResult());
                await tcs.Task;
                break;
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException || ex is System.Net.Sockets.SocketException)
            {
                _logger.LogWarning("RabbitMQ is not reachable in RabbitMqAskJobEventRelayHostedService. Retrying in 5 seconds...");
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
