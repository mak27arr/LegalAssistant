using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;

namespace LegalAssistant.Workers.Embeddings
{
    // RabbitMQ-based embedding service. Uses RPC pattern to get embeddings through queues.
    public class RabbitMqEmbeddingService : IEmbeddingService, IDisposable
    {
        private IConnection? _connection;
        private IModel? _channel;
        private string? _replyQueueName;
        private EventingBasicConsumer? _consumer;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<float[]>> _pendingRequests = new();
        private readonly string _requestQueueName = "embeddings:requests";
        private readonly ConnectionFactory _factory;
        private readonly object _lock = new();

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
                        _replyQueueName = _channel.QueueDeclare().QueueName;
                        _consumer = new EventingBasicConsumer(_channel);
                        _consumer.Received += (model, ea) =>
                        {
                            if (_pendingRequests.TryRemove(ea.BasicProperties.CorrelationId, out var tcs))
                            {
                                var body = ea.Body.ToArray();
                                var vector = JsonSerializer.Deserialize<float[]>(Encoding.UTF8.GetString(body));
                                tcs.SetResult(vector ?? Array.Empty<float>());
                            }
                        };
                        _channel.BasicConsume(queue: _replyQueueName, autoAck: true, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: _consumer);
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

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();

            EnsureConnection();

            var correlationId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<float[]>();
            _pendingRequests[correlationId] = tcs;

            var props = _channel.CreateBasicProperties();
            props.CorrelationId = correlationId;
            props.ReplyTo = _replyQueueName;

            var body = Encoding.UTF8.GetBytes(text);
            _channel.BasicPublish(exchange: "", routingKey: _requestQueueName, mandatory: false, basicProperties: props, body: body);

            // Wait for response with timeout
            using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingRequests.TryRemove(correlationId, out _);
                throw new TimeoutException("Embedding request timed out over RabbitMQ");
            }

            return await tcs.Task;
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
