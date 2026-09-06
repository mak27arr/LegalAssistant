using LegalAssistant.Application.Embeddings;
using LegalAssistant.Core.Correlation;
using LegalAssistant.Messaging;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqEmbeddingRequestPublisher : IEmbeddingEnqueueService
{
    private readonly ICorrelationContext _correlation;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<RabbitMqEmbeddingRequestPublisher> _logger;

    public RabbitMqEmbeddingRequestPublisher(
        ICorrelationContext correlation,
        IRabbitMqPublisher publisher,
        ILogger<RabbitMqEmbeddingRequestPublisher> logger)
    {
        _correlation = correlation;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task EnqueueEmbeddingAsync(
        Guid chunkId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var correlationId = string.IsNullOrWhiteSpace(_correlation.CorrelationId)
            ? chunkId.ToString("N")
            : _correlation.CorrelationId!;

        _correlation.CorrelationId = correlationId;

        await _publisher.PublishAsync(
            new RabbitMqPublishAddress(string.Empty, EmbeddingsRabbitMqTopology.RequestsQueue),
            new { chunkId, text },
            new RabbitMqMessageMetadata
            {
                MessageId = chunkId.ToString("N"),
                CorrelationId = correlationId,
                MessageType = "embedding.requested"
            },
            cancellationToken);

        _logger.LogInformation(
            "Published embeddings:requests. chunkId={ChunkId} correlationId={CorrelationId}",
            chunkId,
            correlationId);
    }
}
