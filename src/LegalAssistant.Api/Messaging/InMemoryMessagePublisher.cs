using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace LegalAssistant.Api.Messaging
{
    public class InMemoryMessagePublisher : IMessagePublisher
    {
        private readonly ILogger<InMemoryMessagePublisher> _logger;
        public static ConcurrentQueue<(string topic, string key, string payload)> Queue = new ConcurrentQueue<(string, string, string)>();

        public InMemoryMessagePublisher(ILogger<InMemoryMessagePublisher> logger)
        {
            _logger = logger;
        }

        public Task PublishAsync(string topic, string key, string payload)
        {
            _logger.LogInformation("Publishing message to {Topic} key={Key}", topic, key);
            Queue.Enqueue((topic, key, payload));
            return Task.CompletedTask;
        }
        }

    public class RabbitMqPublisher : IMessagePublisher
    {
        private readonly ILogger<RabbitMqPublisher> _logger;
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;

        public RabbitMqPublisher(ILogger<RabbitMqPublisher> logger)
        {
            _logger = logger;
            _host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
            _port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var p) ? p : 5672;
            _user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
            _pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";
        }

        public Task PublishAsync(string topic, string key, string payload)
        {
            var factory = new ConnectionFactory() { HostName = _host, Port = _port, UserName = _user, Password = _pass };
            using var conn = factory.CreateConnection();
            using var ch = conn.CreateModel();
            ch.ExchangeDeclare(topic, ExchangeType.Fanout, durable: true);
            var body = Encoding.UTF8.GetBytes(payload);
            var props = ch.CreateBasicProperties();
            props.Persistent = true;
            if (!string.IsNullOrEmpty(key)) 
                props.CorrelationId = key;

            ch.BasicPublish(exchange: topic, routingKey: "", basicProperties: props, body: body);
            _logger.LogInformation("Published to RabbitMQ exchange {Topic} correlation={Corr}", topic, props.CorrelationId);
            return Task.CompletedTask;
        }
    }
}
