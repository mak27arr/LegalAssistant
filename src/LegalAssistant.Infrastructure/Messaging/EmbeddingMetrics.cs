using System.Diagnostics.Metrics;

namespace LegalAssistant.Infrastructure.Messaging;

public static class EmbeddingMetrics
{
    private static readonly Meter Meter = new("LegalAssistant.Embeddings");

    public static readonly Counter<long> DeadLetteredRequestMessages = Meter.CreateCounter<long>(
        "embeddings.requests.dead_lettered",
        unit: "messages",
        description: "Embedding request messages sent to the dead-letter queue.");

    public static readonly Counter<long> DeadLetteredCompletedMessages = Meter.CreateCounter<long>(
        "embeddings.completed.dead_lettered",
        unit: "messages",
        description: "Embedding completion messages sent to the dead-letter queue.");

    public static readonly Counter<long> InvalidRequestMessages = Meter.CreateCounter<long>(
        "embeddings.requests.invalid",
        unit: "messages",
        description: "Invalid embedding request messages.");

    public static readonly Counter<long> InvalidCompletedMessages = Meter.CreateCounter<long>(
        "embeddings.completed.invalid",
        unit: "messages",
        description: "Invalid embedding completion messages.");
}
