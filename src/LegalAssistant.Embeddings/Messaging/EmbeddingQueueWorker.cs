using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Embeddings.Services;
using LegalAssistant.Infrastructure.Messaging;
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

                EmbeddingsRabbitMqTopology.EnsureAll(connection);
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
                _logger.LogInformation("Cancellation requested; requeueing message");
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                return;
            }

            var correlationId = RabbitMqCorrelation.TryGetCorrelationId(ea.BasicProperties?.Headers)
                    ?? ea.BasicProperties?.CorrelationId
                    ?? Guid.NewGuid().ToString("N");
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["correlationId"] = correlationId,
            });

            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var request = TryDeserializeIncoming(json, ea.BasicProperties?.CorrelationId);
            if (request.ChunkId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
            {
                _logger.LogWarning(
                    "Invalid embedding request; acking message. chunkId={ChunkId} textLength={TextLength} rawLength={RawLength} rawStart={RawStart}",
                    request.ChunkId,
                    request.Text?.Length ?? 0,
                    json.Length,
                    json.Length <= 256 ? json : json.Substring(0, 256));
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            _logger.LogInformation("Processing embedding request. textLength={TextLength}", request.Text.Length);

            var vector = await _generator.GenerateAsync(request.Text, stoppingToken);
            if (vector.Length == 0)
            {
                _logger.LogWarning("Generated empty embedding vector; dead-lettering request message");
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                return;
            }
            var completed = new EmbeddingCompletedMessage(request.ChunkId, vector);
            var completedBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(completed));

            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.Headers ??= new System.Collections.Generic.Dictionary<string, object>();
            RabbitMqCorrelation.SetCorrelationId(props.Headers, correlationId);
            props.CorrelationId = correlationId;

            _logger.LogInformation("Publishing embeddings:completed. VectorDimensions={Dimensions}", vector.Length);
            channel.BasicPublish(exchange: "", routingKey: "embeddings:completed", mandatory: false, basicProperties: props, body: completedBody);
            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (OperationCanceledException)
        {
            // If we are shutting down, requeue.
            _logger.LogInformation("Operation canceled; requeueing message");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling embedding queue message");
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static IncomingEmbeddingRequest TryDeserializeIncoming(string json, string? correlationId)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var dto = JsonSerializer.Deserialize<IncomingEmbeddingRequestDto>(json, options);
            if (dto == null)
                return TryCorrelationAsChunkId(json, correlationId);

            var chunkId = dto.ChunkId == Guid.Empty ? dto.Chunk_id : dto.ChunkId;
            var text = dto.Text;
            if (chunkId == Guid.Empty)
                return TryCorrelationAsChunkId(text ?? string.Empty, correlationId);

            return new IncomingEmbeddingRequest(chunkId, text ?? string.Empty);
        }
        catch
        {
            return TryCorrelationAsChunkId(json, correlationId);
        }
    }

    private static IncomingEmbeddingRequest TryCorrelationAsChunkId(string text, string? correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId) && Guid.TryParse(correlationId, out var gid) && gid != Guid.Empty)
            return new IncomingEmbeddingRequest(gid, text);

        return new IncomingEmbeddingRequest(Guid.Empty, text);
    }

    private sealed record IncomingEmbeddingRequest(Guid ChunkId, string Text);
    private sealed class IncomingEmbeddingRequestDto
    {
        [JsonPropertyName("chunkId")]
        public Guid ChunkId { get; init; }

        [JsonPropertyName("chunk_id")]
        public Guid Chunk_id { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
    private sealed record EmbeddingCompletedMessage(Guid ChunkId, float[] Vector);
}
