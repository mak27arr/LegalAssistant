using LegalAssistant.Messaging;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class AskJobRabbitMqTopology : IRabbitMqTopologyDefinition
{
    public const string Exchange = "ask:events";

    public static readonly string[] RoutingKeys =
    [
        "ask.job.queued",
        "ask.job.inprogress",
        "ask.job.completed",
        "ask.job.failed"
    ];

    public void Declare(RabbitMqTopologyBuilder topology) => topology.DeclareExchange(Exchange, "topic");
}
