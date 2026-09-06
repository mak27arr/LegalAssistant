using System.Text.Json;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Embeddings.Services;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IOptions<RabbitMqProcessingOptions> _processingOptions;
    private readonly ILogger<EmbeddingRequestConsumerDefinition> _logger;

    public EmbeddingRequestConsumerDefinition(
        IEmbeddingGenerator generator,
        IRabbitMqPublisher publisher,
        IOptions<RabbitMqProcessingOptions> processingOptions,
        ILogger<EmbeddingRequestConsumerDefinition> logger)
    {
        _generator = generator;
        _publisher = publisher;
        _processingOptions = processingOptions;
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

        var status = scopedServices.GetRequiredService<IEmbeddingStatusService>();

        if (chunkId == Guid.Empty || string.IsNullOrWhiteSpace(request.Text))
        {
            if (chunkId != Guid.Empty)
            {
                await status.RecordFailureAsync(
                    chunkId,
                    "Embedding request did not contain text.",
                    terminal: true,
                    request.JobId,
                    request.ChunkingRunId,
                    cancellationToken);
            }
            EmbeddingMetrics.InvalidRequestMessages.Add(1);
            _logger.LogWarning("Invalid embedding request; dead-lettering message. chunkId={ChunkId}", chunkId);
            return RabbitMqMessageResult.DeadLetter;
        }

        if (!await status.MarkInProgressAsync(chunkId, request.JobId, request.ChunkingRunId, cancellationToken))
        {
            EmbeddingMetrics.DeadLetteredRequestMessages.Add(1);
            _logger.LogWarning("Embedding request references a missing chunk; dead-lettering message. chunkId={ChunkId}", chunkId);
            return RabbitMqMessageResult.DeadLetter;
        }

        _logger.LogInformation("Processing embedding request. chunkId={ChunkId} textLength={TextLength}", chunkId, request.Text.Length);

        try
        {
            var vector = await _generator.GenerateAsync(request.Text, cancellationToken);
            if (vector.Length == 0)
            {
                await status.RecordFailureAsync(
                    chunkId,
                    "Embedding generator returned an empty vector.",
                    terminal: true,
                    request.JobId,
                    request.ChunkingRunId,
                    cancellationToken);
                EmbeddingMetrics.DeadLetteredRequestMessages.Add(1);
                _logger.LogWarning("Generated empty embedding vector; marking chunk failed and dead-lettering request message");
                return RabbitMqMessageResult.DeadLetter;
            }

            await _publisher.PublishAsync(
                new RabbitMqPublishAddress(string.Empty, EmbeddingsRabbitMqTopology.CompletedQueue),
                new EmbeddingCompletedMessage(chunkId, vector, request.JobId, request.ChunkingRunId),
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
        catch (Exception ex)
        {
            var terminal = IsFinalAttempt(context.Metadata.Headers);
            try
            {
                await status.RecordFailureAsync(
                    chunkId,
                    DescribeException(ex),
                    terminal,
                    request.JobId,
                    request.ChunkingRunId,
                    cancellationToken);
            }
            catch (Exception stateException)
            {
                _logger.LogError(stateException, "Could not persist embedding failure state. chunkId={ChunkId}", chunkId);
            }

            if (terminal)
                EmbeddingMetrics.DeadLetteredRequestMessages.Add(1);
            throw;
        }
    }

    private bool IsFinalAttempt(IReadOnlyDictionary<string, object>? headers)
        => RabbitMqRetryPolicy.GetAttempts(headers) + 1 >= Math.Max(1, _processingOptions.Value.MaxAttempts);

    private static string DescribeException(Exception exception)
        => $"{exception.GetType().Name}: {exception.Message}";
}
