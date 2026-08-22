using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Documents.Services;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace LegalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqDocumentIngestJobPublisher : IDocumentIngestJobPublisher, IDisposable
{
    private readonly ILogger<RabbitMqDocumentIngestJobPublisher> _logger;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqDocumentIngestJobPublisher(ILogger<RabbitMqDocumentIngestJobPublisher> logger)
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
        if (_connection != null && _connection.IsOpen) return;

        lock (_lock)
        {
            if (_connection != null && _connection.IsOpen) return;
            _connection?.Dispose();
            _connection = _factory.CreateConnection();
            _channel?.Dispose();
            _channel = _connection.CreateModel();
            _channel.ConfirmSelect();
            IngestRabbitMqTopology.EnsureAll(_connection);
        }
    }

    public Task PublishAsync(Guid jobId, string payload, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            EnsureConnection();

            var body = Encoding.UTF8.GetBytes(payload);
            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = jobId.ToString("N");
            props.CorrelationId = jobId.ToString("N");
            props.Type = DocumentIngestMessageNames.MessageType;

            _channel.BasicPublish(
                exchange: "",
                routingKey: IngestRabbitMqTopology.Queue,
                mandatory: false,
                basicProperties: props,
                body: body);

            if (!_channel.WaitForConfirms())
                throw new InvalidOperationException("RabbitMQ did not confirm ingest job publish.");

            _logger.LogInformation("Published ingest job message. jobId={JobId}", jobId);
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
