using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LegalAssistant.Messaging;

public sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IDisposable
{
    private readonly RabbitMqConnectionOptions _options;
    private readonly ILogger<RabbitMqConnectionProvider> _logger;
    private readonly object _sync = new();
    private IConnection? _connection;

    public RabbitMqConnectionProvider(
        IOptions<RabbitMqConnectionOptions> options,
        ILogger<RabbitMqConnectionProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IConnection GetConnection(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_connection is { IsOpen: true })
                return _connection;

            DisposeConnection();

            var factory = _options.CreateFactory();
            _connection = factory.CreateConnection();
            _connection.ConnectionShutdown += OnConnectionShutdown;

            _logger.LogInformation(
                "RabbitMQ connection established to {Host}:{Port}",
                _options.Host,
                _options.Port);

            return _connection;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            if (_connection is null || _connection.IsOpen)
                return;

            DisposeConnection();
        }
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs args)
        => _logger.LogWarning(
            "RabbitMQ connection shutdown. code={ReplyCode} text={ReplyText}",
            args.ReplyCode,
            args.ReplyText);

    private void DisposeConnection()
    {
        if (_connection is null)
            return;

        _connection.ConnectionShutdown -= OnConnectionShutdown;
        _connection.Dispose();
        _connection = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            DisposeConnection();
        }
    }
}
