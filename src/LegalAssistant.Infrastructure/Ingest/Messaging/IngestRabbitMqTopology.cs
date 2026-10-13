using LegalAssistant.Messaging;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class IngestRabbitMqTopology : IRabbitMqTopologyDefinition
{
    public const string Queue = "ingest:jobs";

    public const string Dlx = "ingest:jobs:dlx";
    public const string Dlq = "ingest:jobs:dlq";

    public void Declare(RabbitMqTopologyBuilder topology)
        => topology.DeclareQueueWithDeadLetter(Queue, Dlx, Dlq, Queue);
}
