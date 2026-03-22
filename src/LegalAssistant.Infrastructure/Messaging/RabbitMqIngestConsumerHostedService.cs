using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqIngestConsumerHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<RabbitMqIngestConsumerHostedService> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqIngestConsumerHostedService(IServiceProvider sp, ILogger<RabbitMqIngestConsumerHostedService> logger)
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

        var factory = new ConnectionFactory { HostName = host, Port = port, UserName = user, Password = pass, AutomaticRecoveryEnabled = true };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection?.Dispose();
                _connection = factory.CreateConnection();
                _channel?.Dispose();
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare("ingest", ExchangeType.Fanout, durable: true);
                var queueName = _channel.QueueDeclare().QueueName;
                _channel.QueueBind(queue: queueName, exchange: "ingest", routingKey: "");

                _logger.LogInformation("RabbitMQ ingest consumer connected to {Host}:{Port}", host, port);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (_, ea) =>
                {
                    var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var corr = ea.BasicProperties?.CorrelationId;
                    _logger.LogInformation("Received ingest message, corrId={Corr}", corr);

                    try
                    {
                        using var scope = _sp.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();

                        if (Guid.TryParse(corr, out var jobId))
                        {
                            var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, stoppingToken);
                            if (job != null)
                            {
                                job.Payload = payload;
                                await db.SaveChangesAsync(stoppingToken);
                            }
                        }

                        _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing ingest message");
                        _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
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
