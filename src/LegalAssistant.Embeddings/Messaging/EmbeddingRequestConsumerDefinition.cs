using System.Text.Json;
using LegalAssistant.Embeddings.Services;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Messaging;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Embeddings.Messaging;

public sealed class EmbeddingRequestConsumerDefinition
    : IRabbitMqConsumerDefinition<EmbeddingRequestMessage>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IEmbeddingGenerator _generator;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<EmbeddingRequestConsumerDefinition> _logger;

    public EmbeddingRequestConsumerDefinition(
        IEmbeddingGenerator generator,
        IRabbitMqPublisher publisher,
        ILogger<EmbeddingRequestConsumerDefinition> logger)
    {
        _generator = generator;
        _publisher = publisher;
        _logger = logger;
    }

    public RabbitMqConsumerEndpoint Endpoint { get; } = new(EmbeddingsRabbitMqTopology.RequestsQueue)
    {
        PrefetchCount = 1,
        DeadLetter = new RabbitMqDeadLetterDefinition(
            EmbeddingsRabbitMqTopology.RequestsDlx,
            EmbeddingsRabbitMqTopology.RequestsDlq,
            EmbeddingsRabbitMqTopology.RequestsQueue)
    };

    public EmbeddingRequestMessage Deserialize(ReadOnlyMemory<byte> body) => JsonSerializer.Deserialize<EmbeddingRequestMessage>(body.Span, SerializerOptions)
           ?? throw new JsonException("Embedding request payload was empty.");

    public async Task<RabbitMqMessageResult> HandleAsync(
        IServiceProvider scopedServices,
        RabbitMqMessageContext<EmbeddingRequestMessage> context,
        CancellationToken cancellationToken)
    {
        var request = context.Message;
        var chunkId = request.EffectiveChunkId;
        var correlationId = context.Metadata.CorrelationId ?? chunkId.ToString("N");

        if (chunkId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
        {
            _logger.LogWarning("Invalid embedding request; dead-lettering message. chunkId={ChunkId}", chunkId);
            return RabbitMqMessageResult.DeadLetter;
        }

        _logger.LogInformation("Processing embedding request. chunkId={ChunkId} textLength={TextLength}", chunkId, request.Text.Length);

        var vector = await _generator.GenerateAsync(request.Text, cancellationToken);
        if (vector.Length == 0)
        {
            _logger.LogWarning("Generated empty embedding vector; dead-lettering request message");
            return RabbitMqMessageResult.DeadLetter;
        }

        await _publisher.PublishAsync(
            new RabbitMqPublishAddress(string.Empty, EmbeddingsRabbitMqTopology.CompletedQueue),
            new EmbeddingCompletedMessage(chunkId, vector),
            new RabbitMqMessageMetadata
            {
                MessageId = $"{chunkId:N}:completed",
                CorrelationId = correlationId,
                MessageType = "embedding.completed"
            },
            cancellationToken);

        _logger.LogInformation("Published embeddings:completed. chunkId={ChunkId} vectorDimensions={Dimensions}", chunkId, vector.Length);

        return RabbitMqMessageResult.Ack;
    }
}
