using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace LegalAssistant.Infrastructure.Messaging;

public static class RabbitMqRetryPublisher
{
    public static void EnsureRetryInfrastructure(IModel channel)
    {
        channel.ExchangeDeclare("retry:delayed", ExchangeType.Direct, durable: true);
        channel.QueueDeclare(queue: "retry:delayed", durable: true, exclusive: false, autoDelete: false, arguments: null);
        channel.QueueBind(queue: "retry:delayed", exchange: "retry:delayed", routingKey: "retry:delayed");
    }

    public static Task PublishDelayedAsync(
        IModel channel,
        string destQueue,
        ReadOnlyMemory<byte> body,
        string? correlationId,
        IDictionary<string, object>? headers,
        int delaySeconds,
        CancellationToken cancellationToken)
    {
        // Uses per-message expiration + DLX to route back to destination queue.
        var retryDlx = "retry:dlx";
        var retryQueue = $"retry:{destQueue}";

        channel.ExchangeDeclare(retryDlx, ExchangeType.Direct, durable: true);
        channel.QueueDeclare(
            queue: retryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = destQueue
            });
        channel.QueueBind(queue: retryQueue, exchange: retryDlx, routingKey: destQueue);

        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.CorrelationId = correlationId;
        props.Headers = headers == null ? new Dictionary<string, object>() : new Dictionary<string, object>(headers);
        props.Expiration = (delaySeconds <= 0 ? 0 : delaySeconds * 1000).ToString();

        channel.BasicPublish(exchange: retryDlx, routingKey: destQueue, mandatory: false, basicProperties: props, body: body);
        return Task.CompletedTask;
    }
}
