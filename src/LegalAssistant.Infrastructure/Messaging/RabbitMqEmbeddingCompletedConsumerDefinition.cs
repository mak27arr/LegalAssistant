using System.Text.Json;
using LegalAssistant.Application.Common;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Logging.Correlation;
using LegalAssistant.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqEmbeddingCompletedConsumerDefinition
    : IRabbitMqConsumerDefinition<EmbeddingCompletedMessage>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<RabbitMqEmbeddingCompletedConsumerDefinition> _logger;

    public RabbitMqEmbeddingCompletedConsumerDefinition(
        ILogger<RabbitMqEmbeddingCompletedConsumerDefinition> logger)
    {
        _logger = logger;
    }

    public RabbitMqConsumerEndpoint Endpoint { get; } = new(EmbeddingsRabbitMqTopology.CompletedQueue)
    {
        PrefetchCount = 1,
        DeadLetter = new RabbitMqDeadLetterDefinition(
            EmbeddingsRabbitMqTopology.CompletedDlx,
            EmbeddingsRabbitMqTopology.CompletedDlq,
            EmbeddingsRabbitMqTopology.CompletedQueue)
    };

    public EmbeddingCompletedMessage Deserialize(ReadOnlyMemory<byte> body)
        => JsonSerializer.Deserialize<EmbeddingCompletedMessage>(body.Span, SerializerOptions)
           ?? throw new JsonException("Embedding completion payload was empty.");

    public async Task<RabbitMqMessageResult> HandleAsync(
        IServiceProvider scopedServices,
        RabbitMqMessageContext<EmbeddingCompletedMessage> context,
        CancellationToken cancellationToken)
    {
        var message = context.Message;
        var correlationId = context.Metadata.CorrelationId ?? message.ChunkId.ToString("N");

        using var correlationScope = CorrelationLogScopeFactory.Create(
            scopedServices,
            _logger,
            correlationId,
            nameof(RabbitMqEmbeddingCompletedConsumerDefinition),
            new Dictionary<string, object?>
            {
                ["chunkId"] = message.ChunkId
            });

        if (message.ChunkId == Guid.Empty || message.Vector is null)
        {
            _logger.LogWarning("Invalid embeddings:completed message; dead-lettering message");
            return RabbitMqMessageResult.DeadLetter;
        }

        if (message.Vector.Length == 0)
        {
            _logger.LogWarning("Received empty embedding vector; acknowledging without persisting");
            return RabbitMqMessageResult.Ack;
        }

        _logger.LogInformation(
            "Received embeddings:completed. vectorDimensions={Dimensions}",
            message.Vector.Length);

        var db = scopedServices.GetRequiredService<LegalAssistantDbContext>();
        var chunk = await db.DocumentChunks.FirstOrDefaultAsync(
            c => c.Id == message.ChunkId,
            cancellationToken);

        if (chunk is null)
        {
            _logger.LogWarning("Chunk not found for embeddings:completed message");
            return RabbitMqMessageResult.Ack;
        }

        chunk.Embedding = new EmbeddingVector(message.Vector);
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Embedding persisted for chunk");
        return RabbitMqMessageResult.Ack;
    }
}

public sealed record EmbeddingCompletedMessage(Guid ChunkId, float[]? Vector);
