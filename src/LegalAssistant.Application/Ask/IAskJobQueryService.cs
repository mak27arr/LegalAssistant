using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobQueryService
{
    Task<AskJobDetails?> GetByIdAsync(Guid jobId, Guid ownerUserId, CancellationToken cancellationToken = default);
}
