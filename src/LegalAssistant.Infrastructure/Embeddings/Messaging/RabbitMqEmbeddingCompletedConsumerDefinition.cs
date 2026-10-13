using System.Text.Json;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Messaging;
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

    public RabbitMqEmbeddingCompletedConsumerDefinition(ILogger<RabbitMqEmbeddingCompletedConsumerDefinition> logger)
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

        var status = scopedServices.GetRequiredService<IEmbeddingStatusService>();

        if (message.ChunkId == Guid.Empty || message.Vector is null)
        {
            if (message.ChunkId != Guid.Empty)
            {
                await status.RecordFailureAsync(
                    message.ChunkId,
                    "Embedding completion message did not contain a vector.",
                    terminal: true,
                    message.JobId,
                    message.ChunkingRunId,
                    cancellationToken);
            }
            EmbeddingMetrics.InvalidCompletedMessages.Add(1);
            _logger.LogWarning("Invalid embeddings:completed message; dead-lettering message");
            return RabbitMqMessageResult.DeadLetter;
        }

        if (message.Vector.Length == 0)
        {
            await status.RecordFailureAsync(
                message.ChunkId,
                "Embedding completion contained an empty vector.",
                terminal: true,
                message.JobId,
                message.ChunkingRunId,
                cancellationToken);
            EmbeddingMetrics.DeadLetteredCompletedMessages.Add(1);
            _logger.LogWarning("Received empty embedding vector; marking chunk failed and dead-lettering message");
            return RabbitMqMessageResult.DeadLetter;
        }

        _logger.LogInformation(
            "Received embeddings:completed. vectorDimensions={Dimensions}",
            message.Vector.Length);

        var result = await status.MarkCompletedAsync(
            message.ChunkId,
            message.Vector,
            message.JobId,
            message.ChunkingRunId,
            cancellationToken);

        if (!result.ChunkFound)
        {
            EmbeddingMetrics.DeadLetteredCompletedMessages.Add(1);
            _logger.LogWarning("Chunk not found for embedding completion; dead-lettering. chunkId={ChunkId} jobId={JobId} runId={RunId} correlationId={CorrelationId}",
                message.ChunkId, message.JobId, message.ChunkingRunId, correlationId);
            return RabbitMqMessageResult.DeadLetter;
        }

        _logger.LogInformation(
            "Embedding persisted for chunk. chunkId={ChunkId} jobId={JobId} runId={RunId} correlationId={CorrelationId} runStatus={RunStatus} jobStatus={JobStatus}",
            message.ChunkId,
            message.JobId,
            message.ChunkingRunId,
            correlationId,
            result.RunFailed ? "Failed" : result.RunCompleted ? "Completed" : "EmbeddingInProgress",
            result.JobStatus);
        return RabbitMqMessageResult.Ack;
    }
}

public sealed record EmbeddingCompletedMessage(
    Guid ChunkId,
    float[]? Vector,
    Guid? JobId = null,
    Guid? ChunkingRunId = null);
