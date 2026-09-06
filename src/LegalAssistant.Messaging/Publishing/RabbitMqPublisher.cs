using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Messaging;

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly object _sync = new();
    private RabbitMQ.Client.IModel? _channel;

    public RabbitMqPublisher(
        IRabbitMqConnectionProvider connectionProvider,
        ILogger<RabbitMqPublisher> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    public Task PublishAsync<T>(
        RabbitMqPublishAddress address,
        T message,
        RabbitMqMessageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        return PublishRawAsync(address, body, metadata, cancellationToken);
    }

    public Task PublishRawAsync(
        RabbitMqPublishAddress address,
        ReadOnlyMemory<byte> body,
        RabbitMqMessageMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            try
            {
                var channel = GetChannel(cancellationToken);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = metadata.Persistent;
                properties.MessageId = metadata.MessageId ?? Guid.NewGuid().ToString("N");
                properties.CorrelationId = metadata.CorrelationId;
                properties.Type = metadata.MessageType;
                properties.ContentType = metadata.ContentType;
                properties.Expiration = metadata.Expiration;
                properties.Headers = metadata.Headers is null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(metadata.Headers);

                channel.BasicPublish(
                    address.Exchange,
                    address.RoutingKey,
                    address.Mandatory,
                    properties,
                    body);

                if (!channel.WaitForConfirms())
                    throw new InvalidOperationException($"RabbitMQ did not confirm publish to '{address.Exchange}:{address.RoutingKey}'.");

                _logger.LogDebug(
                    "RabbitMQ message published. exchange={Exchange} routingKey={RoutingKey} messageId={MessageId} correlationId={CorrelationId}",
                    address.Exchange,
                    address.RoutingKey,
                    properties.MessageId,
                    properties.CorrelationId);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "RabbitMQ publish failed. exchange={Exchange} routingKey={RoutingKey} correlationId={CorrelationId}",
                    address.Exchange,
                    address.RoutingKey,
                    metadata.CorrelationId);
                _connectionProvider.Reset();
                DisposeChannel();
                throw;
            }
        }
    }

    private RabbitMQ.Client.IModel GetChannel(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        DisposeChannel();
        var connection = _connectionProvider.GetConnection(cancellationToken);
        _channel = connection.CreateModel();
        _channel.ConfirmSelect();
        return _channel;
    }

    private void DisposeChannel()
    {
        _channel?.Dispose();
        _channel = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            DisposeChannel();
        }
    }
}
