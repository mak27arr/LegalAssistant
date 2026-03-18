using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Embeddings.Contracts;
using LegalAssistant.Embeddings.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LegalAssistant.Embeddings.Messaging;

public sealed class EmbeddingRpcWorker : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly IEmbeddingGenerator _generator;
    private readonly ILogger<EmbeddingRpcWorker> _logger;

    public EmbeddingRpcWorker(RabbitMqOptions options, IEmbeddingGenerator generator, ILogger<EmbeddingRpcWorker> logger)
    {
        _options = options;
        _generator = generator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    UserName = _options.User,
                    Password = _options.Pass,
                    AutomaticRecoveryEnabled = true
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                channel.QueueDeclare(queue: _options.QueueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, 1, false);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += ReceivedHandler(channel);

                channel.BasicConsume(queue: _options.QueueName, autoAck: false, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: consumer);

                _logger.LogInformation("Embedding RPC worker listening on queue {Queue}", _options.QueueName);

                var tcs = new TaskCompletionSource();
                using var reg = stoppingToken.Register(() => tcs.TrySetResult());
                await tcs.Task;

                break;
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException || ex is System.Net.Sockets.SocketException)
            {
                _logger.LogWarning("RabbitMQ is not reachable in Embeddings Service. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private EventHandler<BasicDeliverEventArgs> ReceivedHandler(IModel channel)
    {
        return (model, ea) =>
        {
            try
            {
                var props = ea.BasicProperties;
                var replyProps = channel.CreateBasicProperties();
                replyProps.CorrelationId = props.CorrelationId;

                var body = ea.Body.ToArray();
                var requestText = Encoding.UTF8.GetString(body);

                var request = TryDeserializeRequest(requestText) ?? new EmbeddingRequest(requestText);
                var vector = _generator.Generate(request.Text);
                var response = new EmbeddingResponse(vector);

                var responseBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response.Vector));
                channel.BasicPublish(exchange: "", routingKey: props.ReplyTo, mandatory: false, basicProperties: replyProps, body: responseBody);
                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling embedding RPC message");
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };
    }

    private static EmbeddingRequest? TryDeserializeRequest(string requestText)
    {
        try
        {
            return JsonSerializer.Deserialize<EmbeddingRequest>(requestText);
        }
        catch
        {
            return null;
        }
    }
}
