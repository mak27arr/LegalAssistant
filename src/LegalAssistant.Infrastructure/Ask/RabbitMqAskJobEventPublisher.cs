using System.Text;
using System.Text.Json;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace LegalAssistant.Infrastructure.Ask;

public sealed class RabbitMqAskJobEventPublisher : IAskJobEventPublisher, IDisposable
{
    private readonly ILogger<RabbitMqAskJobEventPublisher> _logger;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqAskJobEventPublisher(ILogger<RabbitMqAskJobEventPublisher> logger)
    {
        _logger = logger;

        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        var port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var p) ? p : 5672;
        var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";

        _factory = new ConnectionFactory { HostName = host, Port = port, UserName = user, Password = pass, AutomaticRecoveryEnabled = true };
    }

    private void EnsureConnection()
    {
        if (_connection != null && _connection.IsOpen)
            return;

        lock (_lock)
        {
            if (_connection != null && _connection.IsOpen)
                return;

            _connection?.Dispose();
            _connection = _factory.CreateConnection();
            _channel?.Dispose();
            _channel = _connection.CreateModel();
            _channel.ConfirmSelect();
            AskJobRabbitMqTopology.EnsureExchange(_connection);
        }
    }

    public Task PublishAsync(AskJobEventRecord eventRecord, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            EnsureConnection();

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventRecord));
            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = eventRecord.Id.ToString();
            props.CorrelationId = eventRecord.JobId.ToString("N");
            props.Type = AskJobMessageNames.GetMessageType(eventRecord.Status);

            _channel.BasicPublish(
                exchange: AskJobRabbitMqTopology.Exchange,
                routingKey: AskJobMessageNames.GetRoutingKey(eventRecord.Status),
                mandatory: false,
                basicProperties: props,
                body: body);

            if (!_channel.WaitForConfirms())
                throw new InvalidOperationException("RabbitMQ did not confirm ask event publish.");

            _logger.LogInformation("Published ask job event. jobId={JobId} eventId={EventId} status={Status}", eventRecord.JobId, eventRecord.Id, eventRecord.Status);
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
