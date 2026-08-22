using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Messaging;

public interface IMessageOutboxWriter
{
    Task AddAsync(OutboxMessageRecord message, CancellationToken cancellationToken = default);
}
