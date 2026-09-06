using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class DocumentIngestOutboxPublisher : IOutboxMessagePublisher
{
    private readonly IDocumentIngestJobPublisher _publisher;

    public DocumentIngestOutboxPublisher(IDocumentIngestJobPublisher publisher)
    {
        _publisher = publisher;
    }

    public IReadOnlyCollection<string> MessageTypes { get; } =
        [DocumentIngestMessageNames.MessageType];

    public Task PublishAsync(OutboxMessageRecord message, CancellationToken cancellationToken = default)
        => _publisher.PublishAsync(
            message.JobId ?? throw new InvalidOperationException(
                $"Ingest outbox message has no job id. outboxId={message.Id}"),
            message.Payload,
            cancellationToken);
}
