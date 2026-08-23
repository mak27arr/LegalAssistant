using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace LegalAssistant.Infrastructure.Ask;

public static class AskJobRabbitMqTopology
{
    public const string Exchange = "ask:events";

    public static readonly string[] RoutingKeys =
    [
        "ask.job.queued",
        "ask.job.inprogress",
        "ask.job.completed",
        "ask.job.failed"
    ];

    public static void EnsureExchange(IConnection connection)
    {
        using var channel = connection.CreateModel();

        try
        {
            channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null);
        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException(
                $"RabbitMQ topology precondition failed for exchange '{Exchange}'.",
                ex);
        }
    }
}
