using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Logging.Correlation;
using LegalAssistant.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqIngestConsumerDefinition
    : IRabbitMqConsumerDefinition<ReadOnlyMemory<byte>>
{
    private readonly ILogger<RabbitMqIngestConsumerDefinition> _logger;

    public RabbitMqIngestConsumerDefinition(ILogger<RabbitMqIngestConsumerDefinition> logger)
    {
        _logger = logger;
    }

    public RabbitMqConsumerEndpoint Endpoint { get; } = new(IngestRabbitMqTopology.Queue)
    {
        PrefetchCount = 1,
        DeadLetter = new RabbitMqDeadLetterDefinition(
            IngestRabbitMqTopology.Dlx,
            IngestRabbitMqTopology.Dlq,
            IngestRabbitMqTopology.Queue)
    };

    public ReadOnlyMemory<byte> Deserialize(ReadOnlyMemory<byte> body) => body;

    public async Task<RabbitMqMessageResult> HandleAsync(
        IServiceProvider scopedServices,
        RabbitMqMessageContext<ReadOnlyMemory<byte>> context,
        CancellationToken cancellationToken)
    {
        var correlationId = context.Metadata.CorrelationId;
        _logger.LogInformation("Received ingest message. correlationId={CorrelationId}", correlationId);

        if (!Guid.TryParse(correlationId, out var jobId) || jobId == Guid.Empty)
        {
            _logger.LogWarning(
                "Invalid ingest message correlation ID; dead-lettering message. correlationId={CorrelationId}",
                correlationId);
            return RabbitMqMessageResult.DeadLetter;
        }

        using var correlationScope = CorrelationLogScopeFactory.Create(
            scopedServices,
            _logger,
            correlationId,
            nameof(RabbitMqIngestConsumerDefinition));

        var processor = scopedServices.GetRequiredService<IIngestJobProcessor>();
        await processor.ProcessAsync(jobId, cancellationToken);
        return RabbitMqMessageResult.Ack;
    }
}
