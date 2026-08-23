using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Models;

public static class AskJobStatusExtensions
{
    public static bool IsTerminal(this AskJobStatus status)
        => status is AskJobStatus.Completed or AskJobStatus.Failed;
}
