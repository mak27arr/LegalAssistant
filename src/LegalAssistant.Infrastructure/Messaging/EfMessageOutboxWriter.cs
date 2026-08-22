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
        => _db.OutboxMessages.AddAsync(message, cancellationToken).AsTask();
}
