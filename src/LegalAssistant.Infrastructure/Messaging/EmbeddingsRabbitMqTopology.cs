using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace LegalAssistant.Infrastructure.Messaging;

public static class EmbeddingsRabbitMqTopology
{
    public const string RequestsQueue = "embeddings:requests";
    public const string CompletedQueue = "embeddings:completed";

    public const string RequestsDlx = "embeddings:requests:dlx";
    public const string RequestsDlq = "embeddings:requests:dlq";

    public const string CompletedDlx = "embeddings:completed:dlx";
    public const string CompletedDlq = "embeddings:completed:dlq";

    public static void EnsureRequests(IConnection connection) =>
        EnsureQueueWithDlq(connection, RequestsQueue, RequestsDlx, RequestsDlq, RequestsQueue);

    public static void EnsureCompleted(IConnection connection) =>
        EnsureQueueWithDlq(connection, CompletedQueue, CompletedDlx, CompletedDlq, CompletedQueue);

    public static void EnsureAll(IConnection connection)
    {
        EnsureRequests(connection);
        EnsureCompleted(connection);
    }

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
