using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Models;

public enum AskJobStreamItemKind
{
    JobNotFound,
    Event,
    Heartbeat,
    SessionExpired
}

public sealed record AskJobStreamItem(
    AskJobStreamItemKind Kind,
    AskJobEventRecord? EventRecord = null,
    bool IsReplay = false
);
