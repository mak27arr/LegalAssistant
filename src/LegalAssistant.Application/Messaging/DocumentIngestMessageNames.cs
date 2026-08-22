namespace LegalAssistant.Application.Messaging;

public static class DocumentIngestMessageNames
{
    public const string Queue = "ingest:jobs";
    public const string MessageType = "document.ingest.requested";
}
