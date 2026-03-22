using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskService
{
    Task<AskResult> AskAsync(AskQuery query, CancellationToken cancellationToken = default);
}
