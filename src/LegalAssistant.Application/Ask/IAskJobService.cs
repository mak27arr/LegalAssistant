using LegalAssistant.Application.Ask.Models;

namespace LegalAssistant.Application.Ask;

public interface IAskJobService
{
    Task<AskJobSubmissionResult> SubmitAsync(AskJobSubmissionCommand command, CancellationToken cancellationToken = default);
}
