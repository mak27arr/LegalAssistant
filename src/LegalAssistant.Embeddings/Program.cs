var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<RabbitMqEmbeddingWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Endpoint to generate embeddings (mock implementation)
app.MapPost("/embed", (EmbedRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
    {
        return Results.BadRequest("Text is required");
    }

    // Mock implementation: generate a pseudo-random deterministic vector of size 768
    var seed = req.Text.GetHashCode();
    var random = new Random(seed);
    var vector = new float[768];
    for (int i = 0; i < vector.Length; i++)
    {
        vector[i] = (float)random.NextDouble();
    }

    return Results.Ok(vector);
})
.WithName("GetEmbeddings");

app.Run();

public record EmbedRequest(string Text);

// NEW: Worker class to handle queue-based embedding requests
public class RabbitMqEmbeddingWorker : BackgroundService
{
    private readonly ILogger<RabbitMqEmbeddingWorker> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _pass;

    public RabbitMqEmbeddingWorker(ILogger<RabbitMqEmbeddingWorker> logger)
    {
        _logger = logger;
        _host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
        _port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var p) ? p : 5672;
        _user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
        _pass = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new RabbitMQ.Client.ConnectionFactory() { HostName = _host, Port = _port, UserName = _user, Password = _pass };
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                channel.QueueDeclare(queue: "embeddings:requests", durable: false, exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, 1, false);

                var consumer = new RabbitMQ.Client.Events.EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var text = System.Text.Encoding.UTF8.GetString(body);
                    var props = ea.BasicProperties;
                    var replyProps = channel.CreateBasicProperties();
                    replyProps.CorrelationId = props.CorrelationId;

                    _logger.LogInformation("Processing RPC request for text: {TextSnippet}...", text.Length > 20 ? text[..20] : text);

                    var seed = text.GetHashCode();
                    var random = new Random(seed);
                    var vector = new float[768];
                    for (int i = 0; i < vector.Length; i++)
                    {
                        vector[i] = (float)random.NextDouble();
                    }

                    var responseBody = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(vector));
                    channel.BasicPublish(exchange: "", routingKey: props.ReplyTo, mandatory: false, basicProperties: replyProps, body: responseBody);
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                channel.BasicConsume(queue: "embeddings:requests", autoAck: false, consumerTag: "", noLocal: false, exclusive: false, arguments: null, consumer: consumer);

                _logger.LogInformation("RabbitMQ Embedding Worker is listening on embeddings:requests");

                var tcs = new TaskCompletionSource();
                using var reg = stoppingToken.Register(() => tcs.TrySetResult());
                await tcs.Task;
                break;
            }
            catch (Exception ex) when (ex is RabbitMQ.Client.Exceptions.BrokerUnreachableException || ex is System.Net.Sockets.SocketException)
            {
                _logger.LogWarning("RabbitMQ is not reachable in Embeddings Service. Retrying in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
