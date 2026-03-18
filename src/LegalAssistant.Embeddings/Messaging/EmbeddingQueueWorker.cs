using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Embeddings.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegalAssistant.Embeddings.Messaging;

public sealed class EmbeddingQueueWorker : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly IEmbeddingGenerator _generator;
    private readonly ILogger<EmbeddingQueueWorker> _logger;

    public EmbeddingQueueWorker(RabbitMqOptions options, IEmbeddingGenerator generator, ILogger<EmbeddingQueueWorker> logger)
    {
        _options = options;
        _generator = generator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    UserName = _options.User,
                    Password = _options.Pass,
                    AutomaticRecoveryEnabled = true
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                channel.QueueDeclare(queue: _options.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.QueueDeclare(queue: "embeddings:completed", durable: true, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, 1, false);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (_, ea) => _ = HandleAsync(channel, ea, stoppingToken);

                channel.BasicConsume(queue: _options.QueueName, autoAck: false, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: consumer);

                _logger.LogInformation("Embedding queue worker listening on queue {Queue}", _options.QueueName);

                var tcs = new TaskCompletionSource();
                using var reg = stoppingToken.Register(() => tcs.TrySetResult());
                await tcs.Task;

                break;
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException || ex is System.Net.Sockets.SocketException)
            {
                _logger.LogWarning("RabbitMQ is not reachable in Embeddings Service. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IModel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        try
        {
            if (stoppingToken.IsCancellationRequested)
            {
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                return;
            }

            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var request = TryDeserializeIncoming(json);
            if (request.ChunkId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
            {
                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                return;
            }

            var vector = await _generator.GenerateAsync(request.Text, stoppingToken);
            var completed = new EmbeddingCompletedMessage(request.ChunkId, vector);
            var completedBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(completed));

            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.CorrelationId = request.ChunkId.ToString();

            channel.BasicPublish(exchange: "", routingKey: "embeddings:completed", mandatory: false, basicProperties: props, body: completedBody);
            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (OperationCanceledException)
        {
            // If we are shutting down, requeue.
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling embedding queue message");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static IncomingEmbeddingRequest TryDeserializeIncoming(string json)
    {
        try
        {
            var req = JsonSerializer.Deserialize<IncomingEmbeddingRequest>(json);
            return req ?? new IncomingEmbeddingRequest(Guid.Empty, string.Empty);
        }
        catch
        {
            return new IncomingEmbeddingRequest(Guid.Empty, json);
        }
    }

    private sealed record IncomingEmbeddingRequest(Guid ChunkId, string Text);
    private sealed record EmbeddingCompletedMessage(Guid ChunkId, float[] Vector);
}
