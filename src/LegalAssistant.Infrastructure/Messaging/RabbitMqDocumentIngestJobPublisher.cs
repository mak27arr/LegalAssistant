using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Messaging;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqDocumentIngestJobPublisher : IDocumentIngestJobPublisher
{
    private readonly ILogger<RabbitMqDocumentIngestJobPublisher> _logger;
    private readonly IRabbitMqPublisher _publisher;

    public RabbitMqDocumentIngestJobPublisher(
        IRabbitMqPublisher publisher,
        ILogger<RabbitMqDocumentIngestJobPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishAsync(
        Guid jobId,
        string payload,
        CancellationToken cancellationToken = default)
    {
        await _publisher.PublishRawAsync(
            new RabbitMqPublishAddress(string.Empty, IngestRabbitMqTopology.Queue),
            System.Text.Encoding.UTF8.GetBytes(payload),
            new RabbitMqMessageMetadata
            {
                MessageId = jobId.ToString("N"),
                CorrelationId = jobId.ToString("N"),
                MessageType = DocumentIngestMessageNames.MessageType
            },
            cancellationToken);

        _logger.LogInformation("Published ingest job message. jobId={JobId}", jobId);
    }
}
