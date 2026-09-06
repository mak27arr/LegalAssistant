using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace LegalAssistant.Messaging;

public sealed class RabbitMqTopologyBuilder
{
    private readonly IModel _channel;

    public RabbitMqTopologyBuilder(IModel channel)
    {
        _channel = channel;
    }

    public void DeclareExchange(
        string exchangeName,
        string exchangeType,
        bool durable = true,
        bool autoDelete = false,
        IDictionary<string, object>? arguments = null)
    {
        try
        {
            _channel.ExchangeDeclare(exchangeName, exchangeType, durable, autoDelete, arguments);
        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException(
                $"RabbitMQ topology precondition failed for exchange '{exchangeName}'.",
                ex);
        }
    }

    public string DeclareQueue(
        string queueName,
        bool durable = true,
        bool exclusive = false,
        bool autoDelete = false,
        IDictionary<string, object>? arguments = null)
    {
        try
        {
            return _channel.QueueDeclare(queueName, durable, exclusive, autoDelete, arguments).QueueName;
        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException(
                $"RabbitMQ topology precondition failed for queue '{queueName}'. " +
                "The queue may already exist with different arguments.",
                ex);
        }
    }

    public void BindQueue(string queueName, string exchangeName, string routingKey)
        => _channel.QueueBind(queueName, exchangeName, routingKey);

    public void DeclareQueueWithDeadLetter(
        string queueName,
        string deadLetterExchange,
        string deadLetterQueue,
        string deadLetterRoutingKey)
    {
        DeclareExchange(deadLetterExchange, ExchangeType.Direct);
        DeclareQueue(deadLetterQueue);
        BindQueue(deadLetterQueue, deadLetterExchange, deadLetterRoutingKey);

        DeclareQueue(
            queueName,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange,
                ["x-dead-letter-routing-key"] = deadLetterRoutingKey
            });
    }

    public string DeclareEndpoint(RabbitMqConsumerEndpoint endpoint)
    {
        if (!string.IsNullOrWhiteSpace(endpoint.ExchangeName))
        {
            DeclareExchange(endpoint.ExchangeName!, endpoint.ExchangeType);
        }

        var arguments = endpoint.QueueArguments is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(endpoint.QueueArguments);

        if (endpoint.DeadLetter is not null)
        {
            DeclareExchange(endpoint.DeadLetter.ExchangeName, ExchangeType.Direct);
            DeclareQueue(endpoint.DeadLetter.QueueName);
            BindQueue(endpoint.DeadLetter.QueueName, endpoint.DeadLetter.ExchangeName, endpoint.DeadLetter.RoutingKey);
            arguments["x-dead-letter-exchange"] = endpoint.DeadLetter.ExchangeName;
            arguments["x-dead-letter-routing-key"] = endpoint.DeadLetter.RoutingKey;
        }

        var queueName = DeclareQueue(
            endpoint.QueueName,
            endpoint.Durable,
            endpoint.Exclusive,
            endpoint.AutoDelete,
            arguments);

        if (!string.IsNullOrWhiteSpace(endpoint.ExchangeName))
        {
            BindQueue(queueName, endpoint.ExchangeName!, endpoint.BindingRoutingKey);
        }

        if (endpoint.DeclareRetryQueue && !string.IsNullOrWhiteSpace(queueName))
            DeclareRetryQueue(queueName);

        return queueName;
    }

    public void DeclareRetryQueue(string destinationQueue)
    {
        DeclareExchange(RabbitMqRetryPolicy.RetryExchange, ExchangeType.Direct);
        var retryQueue = RabbitMqRetryPolicy.GetRetryQueueName(destinationQueue);
        DeclareQueue(
            retryQueue,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = destinationQueue
            });
        BindQueue(retryQueue, RabbitMqRetryPolicy.RetryExchange, destinationQueue);
    }
}
