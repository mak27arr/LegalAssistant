using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Messaging;

public static class AskJobMessageNames
{
    public const string Prefix = "ask.job.";
    public static readonly string[] MessageTypes = Enum.GetValues<AskJobStatus>()
        .Select(GetRoutingKey)
        .ToArray();

    public static string GetMessageType(AskJobStatus status)
        => GetRoutingKey(status);

    public static string GetRoutingKey(AskJobStatus status)
        => status switch
        {
            AskJobStatus.Queued => "ask.job.queued",
            AskJobStatus.InProgress => "ask.job.inprogress",
            AskJobStatus.Completed => "ask.job.completed",
            AskJobStatus.Failed => "ask.job.failed",
            _ => "ask.job.unknown"
        };

    public static bool IsMessageType(string messageType)
        => messageType.StartsWith(Prefix, StringComparison.Ordinal);
}
