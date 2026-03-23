using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace LegalAssistant.Infrastructure.Messaging;

public static class IngestRabbitMqTopology
{
    public const string Queue = "ingest:jobs";

    public const string Dlx = "ingest:jobs:dlx";
    public const string Dlq = "ingest:jobs:dlq";

    public static void EnsureAll(IConnection connection) =>
        EnsureQueueWithDlq(connection, Queue, Dlx, Dlq, Queue);

    private static void EnsureQueueWithDlq(IConnection connection, string queueName, string dlxName, string dlqName, string dlRoutingKey)
    {
        using var channel = connection.CreateModel();

        try
        {
            channel.ExchangeDeclare(dlxName, ExchangeType.Direct, durable: true);
            channel.QueueDeclare(queue: dlqName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.QueueBind(queue: dlqName, exchange: dlxName, routingKey: dlRoutingKey);

            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object>
                {
                    ["x-dead-letter-exchange"] = dlxName,
                    ["x-dead-letter-routing-key"] = dlRoutingKey
                });
        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException(
                $"RabbitMQ topology precondition failed for queue '{queueName}'. " +
                "Queue likely already exists with different arguments. " +
                "Delete the queue (and its DLQ/DLX bindings) and restart services.",
                ex);
        }
    }
}
