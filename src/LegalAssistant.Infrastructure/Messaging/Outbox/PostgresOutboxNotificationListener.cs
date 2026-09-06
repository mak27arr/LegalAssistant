using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class PostgresOutboxNotificationListener : IOutboxNotificationListener
{
    internal const string ChannelName = "legalassistant_outbox";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly string? _connectionString;
    private readonly ILogger<PostgresOutboxNotificationListener> _logger;
    private readonly Channel<bool> _notifications = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly object _sync = new();
    private Task? _listenTask;

    public PostgresOutboxNotificationListener(
        string? connectionString,
        ILogger<PostgresOutboxNotificationListener> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public void Start(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return;

        lock (_sync)
        {
            _listenTask ??= ListenLoopAsync(cancellationToken);
        }
    }

    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            await Task.Delay(timeout, cancellationToken);
            return false;
        }

        var notificationTask = _notifications.Reader.WaitToReadAsync(cancellationToken).AsTask();
        var timeoutTask = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(notificationTask, timeoutTask);
        if (completed == timeoutTask)
            return false;

        while (_notifications.Reader.TryRead(out _))
        {
            // Notifications are coalesced. One wakeup is enough to claim all
            // currently due rows in the next dispatcher cycle.
        }

        return true;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"LISTEN {ChannelName};";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                _logger.LogInformation("Listening for outbox notifications on channel {Channel}", ChannelName);

                connection.Notification += (_, _) => _notifications.Writer.TryWrite(true);
                while (!cancellationToken.IsCancellationRequested)
                    await connection.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox notification listener disconnected; retrying");
                try
                {
                    await Task.Delay(ReconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
