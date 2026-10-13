using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobRepository
{
    Task AddAsync(AskJobRecord job, CancellationToken cancellationToken = default);
    Task<AskJobRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AskJobRecord?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<AskJobRecord?> GetByIdempotencyKeyAsync(Guid ownerUserId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<AskJobExecutionLease?> TryClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task<AskJobExecutionLease?> TryClaimAsync(Guid id, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(Guid id, Guid leaseId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task<bool> ReleaseInProgressAsync(Guid id, Guid leaseId, CancellationToken cancellationToken = default);
    Task<int> RecoverExpiredAsync(int batchSize, CancellationToken cancellationToken = default);
}
