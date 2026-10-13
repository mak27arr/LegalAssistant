using System.Text.Json;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Embeddings.Services;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Messaging;
using Microsoft.EntityFrameworkCore;
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
    private readonly IOptions<RabbitMqProcessingOptions> _processingOptions;
    private readonly ILogger<EmbeddingRequestConsumerDefinition> _logger;

    public EmbeddingRequestConsumerDefinition(
        IEmbeddingGenerator generator,
        IOptions<RabbitMqProcessingOptions> processingOptions,
        ILogger<EmbeddingRequestConsumerDefinition> logger)
    {
        _generator = generator;
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

        var db = scopedServices.GetRequiredService<LegalAssistantDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var transactionClosed = false;
        try
        {
            // Keep the state transition and the completion outbox insert in the
            // same transaction. If processing fails, neither the InProgress
            // update nor a partial completion message is committed.
            if (!await status.MarkInProgressAsync(chunkId, request.JobId, request.ChunkingRunId, cancellationToken))
            {
                EmbeddingMetrics.DeadLetteredRequestMessages.Add(1);
                _logger.LogWarning("Embedding request references a missing chunk; dead-lettering message. chunkId={ChunkId}", chunkId);
                await transaction.RollbackAsync(cancellationToken);
                transactionClosed = true;
                return RabbitMqMessageResult.DeadLetter;
            }

            _logger.LogInformation("Processing embedding request. chunkId={ChunkId} textLength={TextLength}", chunkId, request.Text.Length);

            var vector = await _generator.GenerateAsync(request.Text, cancellationToken);
            if (vector.Length == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                await transaction.DisposeAsync();
                transactionClosed = true;
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

            var outbox = scopedServices.GetRequiredService<IMessageOutboxWriter>();
            var deduplicationKey = $"{chunkId:N}:completed";
            var alreadyRecorded = await db.OutboxMessages.AnyAsync(
                x => x.Owner == OutboxMessageOwners.Embeddings &&
                     x.MessageType == EmbeddingCompletedMessageNames.MessageType &&
                     x.DeduplicationKey == deduplicationKey,
                cancellationToken);

            if (!alreadyRecorded)
            {
                await outbox.AddAsync(new OutboxMessageRecord
                {
                    Id = Guid.NewGuid(),
                    Owner = OutboxMessageOwners.Embeddings,
                    JobId = request.JobId,
                    DeduplicationKey = deduplicationKey,
                    MessageType = EmbeddingCompletedMessageNames.MessageType,
                    RoutingKey = EmbeddingCompletedMessageNames.Queue,
                    Payload = JsonSerializer.Serialize(
                        new EmbeddingCompletedMessage(chunkId, vector, request.JobId, request.ChunkingRunId)),
                    CorrelationId = correlationId,
                    Status = OutboxMessageStatus.Pending,
                    Attempts = 0,
                    Version = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    NextAttemptAt = DateTime.UtcNow
                }, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            transactionClosed = true;

            _logger.LogInformation("Recorded embeddings:completed in the embeddings outbox. chunkId={ChunkId} vectorDimensions={Dimensions}", chunkId, vector.Length);

            return RabbitMqMessageResult.Ack;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            if (!transactionClosed)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(rollbackException,
                        "Could not roll back embedding request during shutdown; preserving cancellation. chunkId={ChunkId} jobId={JobId} runId={RunId} correlationId={CorrelationId}",
                        chunkId, request.JobId, request.ChunkingRunId, correlationId);
                }
            }

            _logger.LogDebug(ex,
                "Embedding request cancelled during shutdown; not recording a business failure. chunkId={ChunkId} jobId={JobId} runId={RunId} correlationId={CorrelationId}",
                chunkId, request.JobId, request.ChunkingRunId, correlationId);
            throw;
        }
        catch (Exception ex)
        {
            if (!transactionClosed)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollbackException)
                {
                    _logger.LogWarning(rollbackException, "Could not roll back embedding request transaction. chunkId={ChunkId}", chunkId);
                }
            }
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
                _logger.LogWarning(ex,
                    "Embedding request failed and failure status was recorded before transport retry or dead-letter. chunkId={ChunkId} jobId={JobId} runId={RunId} correlationId={CorrelationId} terminal={Terminal}",
                    chunkId, request.JobId, request.ChunkingRunId, correlationId, terminal);
            }
            catch (Exception stateException)
            {
                _logger.LogError(stateException,
                    "Could not persist embedding failure state; preserving the original request exception. chunkId={ChunkId} jobId={JobId} runId={RunId} correlationId={CorrelationId} originalException={OriginalException}",
                    chunkId, request.JobId, request.ChunkingRunId, correlationId, ex);
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
