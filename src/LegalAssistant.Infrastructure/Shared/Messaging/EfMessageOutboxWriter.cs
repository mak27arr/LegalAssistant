using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class EfMessageOutboxWriter : IMessageOutboxWriter
{
    private readonly LegalAssistantDbContext _db;

    public EfMessageOutboxWriter(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(OutboxMessageRecord message, CancellationToken cancellationToken = default)
    {
        var expectedOwner = OutboxMessageOwners.ForMessageType(message.MessageType);
        if (string.IsNullOrWhiteSpace(message.Owner))
            message.Owner = expectedOwner;
        else if (!string.Equals(message.Owner, expectedOwner, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Outbox message type '{message.MessageType}' belongs to '{expectedOwner}', not '{message.Owner}'.");

        return _db.OutboxMessages.AddAsync(message, cancellationToken).AsTask();
    }
}
