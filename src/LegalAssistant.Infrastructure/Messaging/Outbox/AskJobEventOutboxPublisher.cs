using System.Text.Json;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class AskJobEventOutboxPublisher : IOutboxMessagePublisher
{
    private readonly LegalAssistantDbContext _db;
    private readonly IAskJobEventPublisher _publisher;

    public AskJobEventOutboxPublisher(
        LegalAssistantDbContext db,
        IAskJobEventPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public IReadOnlyCollection<string> MessageTypes => AskJobMessageNames.MessageTypes;

    public async Task PublishAsync(
        OutboxMessageRecord message,
        CancellationToken cancellationToken = default)
    {
        var eventRecord = message.AskJobEventId.HasValue
            ? await _db.AskJobEvents
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == message.AskJobEventId.Value, cancellationToken)
            : JsonSerializer.Deserialize<AskJobEventRecord>(message.Payload);

        if (eventRecord is null)
            throw new InvalidOperationException($"Ask outbox event could not be loaded. outboxId={message.Id}");

        await _publisher.PublishAsync(eventRecord, cancellationToken);
    }
}
