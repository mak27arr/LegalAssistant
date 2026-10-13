namespace LegalAssistant.Application.Messaging;

public static class OutboxMessageOwners
{
    public const string Ingest = "worker-ingest";
    public const string Ask = "worker-ask";
    public const string Embeddings = "embeddings";

    public static string ForMessageType(string messageType)
    {
        if (string.Equals(messageType, DocumentIngestMessageNames.MessageType, StringComparison.Ordinal) ||
            string.Equals(messageType, EmbeddingRequestMessageNames.MessageType, StringComparison.Ordinal))
            return Ingest;

        if (AskJobMessageNames.IsMessageType(messageType))
            return Ask;

        if (string.Equals(messageType, EmbeddingCompletedMessageNames.MessageType, StringComparison.Ordinal))
            return Embeddings;

        throw new InvalidOperationException($"No outbox owner is registered for message type '{messageType}'.");
    }

    public static IReadOnlyCollection<string> MessageTypesForOwners(
        IEnumerable<string> owners)
    {
        var ownerSet = owners.ToHashSet(StringComparer.Ordinal);
        return AllMessageTypes
            .Where(messageType => ownerSet.Contains(ForMessageType(messageType)))
            .ToArray();
    }

    public static IReadOnlyCollection<string> AllMessageTypes =>
        [
            DocumentIngestMessageNames.MessageType,
            EmbeddingRequestMessageNames.MessageType,
            EmbeddingCompletedMessageNames.MessageType,
            .. AskJobMessageNames.MessageTypes
        ];
}
