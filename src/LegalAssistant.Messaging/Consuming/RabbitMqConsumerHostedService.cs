using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics.Metrics;

namespace LegalAssistant.Messaging;

public sealed class RabbitMqConsumerHostedService<TMessage> : BackgroundService
{
    private static readonly Meter Metrics = new("LegalAssistant.Messaging");
    private static readonly Counter<long> Reconnects = Metrics.CreateCounter<long>(
        "rabbitmq.consumer.reconnects",
        unit: "connections",
        description: "RabbitMQ consumer reconnect attempts after a consumer failure.");

    private readonly IRabbitMqConsumerDefinition<TMessage> _definition;
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RabbitMqConnectionOptions> _connectionOptions;
    private readonly IOptions<RabbitMqProcessingOptions> _processingOptions;
    private readonly ILogger<RabbitMqConsumerHostedService<TMessage>> _logger;

    public RabbitMqConsumerHostedService(
        IRabbitMqConsumerDefinition<TMessage> definition,
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqPublisher publisher,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqConnectionOptions> connectionOptions,
        IOptions<RabbitMqProcessingOptions> processingOptions,
        ILogger<RabbitMqConsumerHostedService<TMessage>> logger)
    {
        _definition = definition;
        _connectionProvider = connectionProvider;
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _connectionOptions = connectionOptions;
        _processingOptions = processingOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilShutdownAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "RabbitMQ consumer for {Queue} stopped; reconnecting in {DelaySeconds} seconds",
                    _definition.Endpoint.QueueName,
                    _connectionOptions.Value.ReconnectDelay.TotalSeconds);

                _connectionProvider.Reset();
                Reconnects.Add(1, new KeyValuePair<string, object?>("message_type", typeof(TMessage).Name));

                try
                {
                    await Task.Delay(_connectionOptions.Value.ReconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task ConsumeUntilShutdownAsync(CancellationToken stoppingToken)
    {
        var connection = _connectionProvider.GetConnection(stoppingToken);
        using var channel = connection.CreateModel();
        var endpoint = _definition.Endpoint;
        var queueName = new RabbitMqTopologyBuilder(channel).DeclareEndpoint(endpoint);

        channel.BasicQos(0, endpoint.PrefetchCount, false);

        var shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnModelShutdown(object? _, ShutdownEventArgs args)
            => shutdown.TrySetResult(false);
        void OnConnectionShutdown(object? _, ShutdownEventArgs args)
            => shutdown.TrySetResult(false);

        channel.ModelShutdown += OnModelShutdown;
        connection.ConnectionShutdown += OnConnectionShutdown;

        try
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (_, eventArgs)
                => await HandleMessageAsync(channel, queueName, eventArgs, stoppingToken);

            channel.BasicConsume(
                queueName,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: endpoint.Exclusive,
                arguments: null,
                consumer);

            _logger.LogInformation(
                "RabbitMQ consumer listening. queue={Queue} exchange={Exchange} prefetch={Prefetch}",
                queueName,
                endpoint.ExchangeName,
                endpoint.PrefetchCount);

            using var registration = stoppingToken.Register(() => shutdown.TrySetResult(true));
            await shutdown.Task;
        }
        finally
        {
            channel.ModelShutdown -= OnModelShutdown;
            connection.ConnectionShutdown -= OnConnectionShutdown;
        }
    }

    private async Task HandleMessageAsync(
        RabbitMQ.Client.IModel channel,
        string queueName,
        BasicDeliverEventArgs eventArgs,
        CancellationToken stoppingToken)
    {
        var metadata = RabbitMqMessageMetadata.FromProperties(eventArgs.BasicProperties);
        var correlationId = RabbitMqCorrelation.TryGetCorrelationId(metadata.Headers)
                            ?? metadata.CorrelationId
                            ?? metadata.MessageId
                            ?? Guid.NewGuid().ToString("N");
        var headers = metadata.CopyHeaders();
        RabbitMqCorrelation.SetCorrelationId(headers, correlationId);
        metadata = metadata with { CorrelationId = correlationId, Headers = headers };

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId,
            ["queue"] = queueName,
            ["deliveryTag"] = eventArgs.DeliveryTag
        });

        TMessage message;
        try
        {
            message = _definition.Deserialize(eventArgs.Body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ message deserialization failed");
            await CompleteMalformedMessageAsync(channel, queueName, eventArgs, metadata, stoppingToken);
            return;
        }

        RabbitMqMessageResult result;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = new RabbitMqMessageContext<TMessage>(
                message,
                eventArgs.Body,
                metadata,
                eventArgs.DeliveryTag,
                queueName,
                eventArgs.Exchange,
                eventArgs.RoutingKey);

            result = await _definition.HandleAsync(scope.ServiceProvider, context, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("RabbitMQ message handling canceled; requeueing message");
            SafeNack(channel, eventArgs.DeliveryTag, requeue: true);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ message handler failed");
            result = RabbitMqMessageResult.Retry;
        }

        await CompleteAsync(channel, queueName, eventArgs, metadata, result, stoppingToken);
    }

    private async Task CompleteMalformedMessageAsync(
        RabbitMQ.Client.IModel channel,
        string queueName,
        BasicDeliverEventArgs eventArgs,
        RabbitMqMessageMetadata metadata,
        CancellationToken cancellationToken)
    {
        var result = _definition.Endpoint.MalformedMessageBehavior switch
        {
            RabbitMqMalformedMessageBehavior.Ack => RabbitMqMessageResult.Ack,
            RabbitMqMalformedMessageBehavior.Retry => RabbitMqMessageResult.Retry,
            _ => RabbitMqMessageResult.DeadLetter
        };

        await CompleteAsync(
            channel,
            queueName,
            eventArgs,
            metadata,
            result,
            cancellationToken);
    }

    private async Task CompleteAsync(
        RabbitMQ.Client.IModel channel,
        string queueName,
        BasicDeliverEventArgs eventArgs,
        RabbitMqMessageMetadata metadata,
        RabbitMqMessageResult result,
        CancellationToken cancellationToken)
    {
        switch (result)
        {
            case RabbitMqMessageResult.Ack:
                SafeAck(channel, eventArgs.DeliveryTag);
                return;

            case RabbitMqMessageResult.DeadLetter:
                SafeNack(channel, eventArgs.DeliveryTag, requeue: false);
                return;

            case RabbitMqMessageResult.Retry:
                await RetryAsync(channel, queueName, eventArgs, metadata, cancellationToken);
                return;

            default:
                SafeNack(channel, eventArgs.DeliveryTag, requeue: false);
                return;
        }
    }

    private async Task RetryAsync(
        RabbitMQ.Client.IModel channel,
        string queueName,
        BasicDeliverEventArgs eventArgs,
        RabbitMqMessageMetadata metadata,
        CancellationToken cancellationToken)
    {
        var options = _processingOptions.Value;
        var attempt = RabbitMqRetryPolicy.GetAttempts(metadata.Headers) + 1;
        var maxAttempts = Math.Max(1, options.MaxAttempts);

        if (attempt >= maxAttempts)
        {
            _logger.LogWarning("RabbitMQ retry limit reached; dead-lettering message. attempts={Attempts}", attempt);
            SafeNack(channel, eventArgs.DeliveryTag, requeue: false);
            return;
        }

        var headers = metadata.CopyHeaders();
        RabbitMqRetryPolicy.SetAttempts(headers, attempt);
        headers[RabbitMqRetryPolicy.OriginalQueueHeader] = queueName;
        headers[RabbitMqRetryPolicy.OriginalRoutingKeyHeader] = eventArgs.RoutingKey;

        var delaySeconds = RabbitMqRetryPolicy.NextDelaySeconds(attempt, options);
        var retryMetadata = metadata with
        {
            Headers = headers,
            Expiration = Math.Max(0, delaySeconds * 1000).ToString()
        };

        try
        {
            await _publisher.PublishRawAsync(
                RabbitMqRetryPolicy.GetRetryAddress(queueName),
                eventArgs.Body,
                retryMetadata,
                cancellationToken);

            SafeAck(channel, eventArgs.DeliveryTag);
            _logger.LogWarning(
                "RabbitMQ message scheduled for retry. attempts={Attempts} delaySeconds={DelaySeconds}",
                attempt,
                delaySeconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeNack(channel, eventArgs.DeliveryTag, requeue: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ retry publish failed; requeueing original message");
            SafeNack(channel, eventArgs.DeliveryTag, requeue: true);
        }
    }

    private static void SafeAck(RabbitMQ.Client.IModel channel, ulong deliveryTag)
    {
        try { channel.BasicAck(deliveryTag, multiple: false); }
        catch (Exception) when (!channel.IsOpen) { }
    }

    private static void SafeNack(RabbitMQ.Client.IModel channel, ulong deliveryTag, bool requeue)
    {
        try { channel.BasicNack(deliveryTag, multiple: false, requeue); }
        catch (Exception) when (!channel.IsOpen) { }
    }
}
