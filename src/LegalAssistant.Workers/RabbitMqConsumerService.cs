using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LegalAssistant.Workers
{

#if RABBITMQ
    // Simple RabbitMQ consumer service for ingest jobs
    public class RabbitMqConsumerService : BackgroundService
    {
        private readonly ILogger<RabbitMqConsumerService> _logger;
        private readonly IServiceProvider _sp;
        private IConnection? _connection;
        private IModel? _channel;

        public RabbitMqConsumerService(IServiceProvider sp, ILogger<RabbitMqConsumerService> logger)
        {
            _sp = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
            var port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var p) ? p : 5672;
            var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
            var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";

            var factory = new ConnectionFactory() { HostName = host, Port = port, UserName = user, Password = pass, AutomaticRecoveryEnabled = true };

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _connection?.Dispose();
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    _channel.ExchangeDeclare("ingest", ExchangeType.Fanout, durable: true);
                    var queueName = _channel.QueueDeclare().QueueName;
                    _channel.QueueBind(queueName, "ingest", "");

                    _logger.LogInformation("RabbitMQ consumer connected to {Host}:{Port}", host, port);

                    var consumer = new EventingBasicConsumer(_channel);
                    consumer.Received += async (sender, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var payload = Encoding.UTF8.GetString(body);
                        _logger.LogInformation("Received ingest message, corrId={Corr}", ea.BasicProperties?.CorrelationId);

                        try
                        {
                            using var scope = _sp.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
                            // correlation id is job id
                            if (Guid.TryParse(ea.BasicProperties?.CorrelationId, out var jobId))
                            {
                                var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, stoppingToken);
                                if (job != null)
                                {
                                    job.Payload = payload;
                                    await db.SaveChangesAsync(stoppingToken);
                                }
                            }

                            // ack
                            _channel.BasicAck(ea.DeliveryTag, multiple: false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing ingest message");
                            // reject and requeue
                            _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                        }
                    };

                    _channel.BasicConsume(queue: queueName, autoAck: false, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: consumer);

                    // Wait until cancellation
                    var tcs = new TaskCompletionSource();
                    using var reg = stoppingToken.Register(() => tcs.TrySetResult());
                    await tcs.Task;
                    break;
                }
                catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException || ex is System.Net.Sockets.SocketException)
                {
                    _logger.LogWarning("RabbitMQ is not reachable in RabbitMqConsumerService. Retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
#else
    // RabbitMQ consumer is not compiled when RABBITMQ symbol is not defined.
    // This avoids build errors when the RabbitMQ client package is not available.
    public class RabbitMqConsumerService : BackgroundService
    {
        private readonly ILogger<RabbitMqConsumerService> _logger;

        public RabbitMqConsumerService(ILogger<RabbitMqConsumerService> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RabbitMQ consumer disabled (RABBITMQ symbol not defined)");
            return Task.CompletedTask;
        }
    }
#endif
}
