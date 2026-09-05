using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobRepository
{
    Task AddAsync(AskJobRecord job, CancellationToken cancellationToken = default);
    Task<AskJobRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AskJobRecord?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<AskJobRecord?> GetByIdempotencyKeyAsync(Guid ownerUserId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<AskJobRecord?> DequeueQueuedAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkInProgressAsync(Guid id, CancellationToken cancellationToken = default);
}
