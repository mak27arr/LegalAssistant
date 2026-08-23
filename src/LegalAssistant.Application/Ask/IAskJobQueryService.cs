using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobQueryService
{
    Task<AskJobDetails?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}
