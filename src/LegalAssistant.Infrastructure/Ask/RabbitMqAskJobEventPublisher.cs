using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Messaging;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class RabbitMqAskJobEventPublisher : IAskJobEventPublisher
{
    private readonly ILogger<RabbitMqAskJobEventPublisher> _logger;
    private readonly IRabbitMqPublisher _publisher;

    public RabbitMqAskJobEventPublisher(
        IRabbitMqPublisher publisher,
        ILogger<RabbitMqAskJobEventPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishAsync(
        AskJobEventRecord eventRecord,
        CancellationToken cancellationToken = default)
    {
        await _publisher.PublishAsync(
            new RabbitMqPublishAddress(
                AskJobRabbitMqTopology.Exchange,
                AskJobMessageNames.GetRoutingKey(eventRecord.Status)),
            eventRecord,
            new RabbitMqMessageMetadata
            {
                MessageId = eventRecord.Id.ToString(),
                CorrelationId = eventRecord.JobId.ToString("N"),
                MessageType = AskJobMessageNames.GetMessageType(eventRecord.Status)
            },
            cancellationToken);

        _logger.LogInformation(
            "Published ask job event. jobId={JobId} eventId={EventId} status={Status}",
            eventRecord.JobId,
            eventRecord.Id,
            eventRecord.Status);
    }
}
