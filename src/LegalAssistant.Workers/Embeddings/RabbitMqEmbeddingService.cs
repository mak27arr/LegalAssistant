using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace LegalAssistant.Workers.Embeddings
{
    // RabbitMQ-based embedding service (async). Publishes embedding generation requests to a queue.
    public class RabbitMqEmbeddingService : IEmbeddingService, IDisposable
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly ConnectionFactory _factory;
        private readonly object _lock = new();

        private const string RequestQueueName = "embeddings:requests";

        public RabbitMqEmbeddingService()
        {
            var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
            var port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var p) ? p : 5672;
            var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
            var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";

            _factory = new ConnectionFactory() { HostName = host, Port = port, UserName = user, Password = pass, AutomaticRecoveryEnabled = true };
        }

        private void EnsureConnection()
        {
            if (_connection != null && _connection.IsOpen) return;

            lock (_lock)
            {
                if (_connection != null && _connection.IsOpen) return;

                int retries = 0;
                while (retries < 5)
                {
                    try
                    {
                        _connection?.Dispose();
                        _connection = _factory.CreateConnection();
                        _channel = _connection.CreateModel();
                        _channel.QueueDeclare(queue: RequestQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                        return;
                    }
                    catch
                    {
                        retries++;
                        if (retries >= 5) throw;
                        System.Threading.Thread.Sleep(2000);
                    }
                }
            }
        }

        public Task EnqueueEmbeddingAsync(Guid chunkId, string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

            EnsureConnection();

            var payload = JsonSerializer.Serialize(new { chunkId, text });
            var body = Encoding.UTF8.GetBytes(payload);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.CorrelationId = chunkId.ToString();

            _channel.BasicPublish(exchange: "", routingKey: RequestQueueName, mandatory: false, basicProperties: props, body: body);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
