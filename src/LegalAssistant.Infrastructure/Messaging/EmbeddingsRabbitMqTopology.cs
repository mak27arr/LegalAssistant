using LegalAssistant.Messaging;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class EmbeddingsRabbitMqTopology : IRabbitMqTopologyDefinition
{
    public const string RequestsQueue = "embeddings:requests";
    public const string CompletedQueue = "embeddings:completed";

    public const string RequestsDlx = "embeddings:requests:dlx";
    public const string RequestsDlq = "embeddings:requests:dlq";

    public const string CompletedDlx = "embeddings:completed:dlx";
    public const string CompletedDlq = "embeddings:completed:dlq";

    public void Declare(RabbitMqTopologyBuilder topology)
    {
        topology.DeclareQueueWithDeadLetter(RequestsQueue, RequestsDlx, RequestsDlq, RequestsQueue);
        topology.DeclareQueueWithDeadLetter(CompletedQueue, CompletedDlx, CompletedDlq, CompletedQueue);
    }
}
