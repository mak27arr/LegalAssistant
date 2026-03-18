using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegalAssistant.Workers.Embeddings;

public sealed class EmbeddingCompletedConsumer : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<EmbeddingCompletedConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    private const string CompletedQueueName = "embeddings:completed";

    public EmbeddingCompletedConsumer(IServiceProvider sp, ILogger<EmbeddingCompletedConsumer> logger)
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

        var factory = new ConnectionFactory() { HostName = host, Port = port, UserName = user, Password = pass, AutomaticRecoveryEnabled = true };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection?.Dispose();
                _connection = factory.CreateConnection();
                _channel?.Dispose();
                _channel = _connection.CreateModel();

                _channel.QueueDeclare(queue: CompletedQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                _channel.BasicQos(0, 5, false);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += ReceivedHandler(stoppingToken);

                _channel.BasicConsume(queue: CompletedQueueName, autoAck: false, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: consumer);

                _logger.LogInformation("EmbeddingCompletedConsumer listening on {Queue}", CompletedQueueName);

                var tcs = new TaskCompletionSource();
                using var reg = stoppingToken.Register(() => tcs.TrySetResult());
                await tcs.Task;
                break;
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException || ex is System.Net.Sockets.SocketException)
            {
                _logger.LogWarning("RabbitMQ is not reachable in EmbeddingCompletedConsumer. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private EventHandler<BasicDeliverEventArgs> ReceivedHandler(CancellationToken stoppingToken)
    {
        return async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var msg = JsonSerializer.Deserialize<EmbeddingCompletedMessage>(json);
                if (msg == null || msg.ChunkId == Guid.Empty || msg.Vector == null)
                {
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                var chunk = await db.DocumentChunks.FirstOrDefaultAsync(c => c.Id == msg.ChunkId, stoppingToken);
                if (chunk != null)
                {
                    chunk.Embedding = new Vector(msg.Vector);
                    await db.SaveChangesAsync(stoppingToken);
                }

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing embeddings:completed message");
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    private sealed record EmbeddingCompletedMessage(Guid ChunkId, float[] Vector);
}
