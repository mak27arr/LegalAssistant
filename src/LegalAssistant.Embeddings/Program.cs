using LegalAssistant.Logging.DependencyInjection;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<LegalAssistant.Embeddings.Messaging.RabbitMqOptions>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new LegalAssistant.Embeddings.Messaging.RabbitMqOptions
    {
        Host = config["RabbitMq:Host"] ?? Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq",
        Port = int.TryParse(config["RabbitMq:Port"], out var p) ? p : int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var ep) ? ep : 5672,
        User = config["RabbitMq:User"] ?? Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
        Pass = config["RabbitMq:Pass"] ?? Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest",
        QueueName = config["RabbitMq:EmbeddingsQueue"] ?? "embeddings:requests",
    };
});

builder.Services.Configure<LegalAssistant.Infrastructure.Messaging.RabbitMqProcessingOptions>(
    builder.Configuration.GetSection("RabbitMq:Processing"));

builder.Services.AddHttpClient("ollama", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("OLLAMA_BASEURL") ?? "http://ollama:11434";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddSingleton<LegalAssistant.Embeddings.Services.IEmbeddingGenerator>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var mode = (config["Embeddings:Mode"] ?? "ollama").Trim().ToLowerInvariant();
    if (mode == "mock")
        return new LegalAssistant.Embeddings.Services.DeterministicMockEmbeddingGenerator();

    var model = config["Ollama:Model"] ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "nomic-embed-text";
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
    var logger = sp.GetRequiredService<ILogger<LegalAssistant.Embeddings.Services.OllamaEmbeddingGenerator>>();
    return new LegalAssistant.Embeddings.Services.OllamaEmbeddingGenerator(http, model, logger);
});

builder.Services.AddHostedService<LegalAssistant.Embeddings.Messaging.EmbeddingQueueWorker>();

// Centralized logging registration for container (file JSON logs for sidecar/Filebeat)
// Centralized logging registration for container (file JSON logs for sidecar/Filebeat)
builder.Services.AddCentralizedLogging(builder.Configuration, "embeddings");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Request timing middleware - can be toggled via Logging:RequestTiming:Enabled
var enableRequestTiming = builder.Configuration.GetValue<bool?>("Logging:RequestTiming:Enabled") ?? true;
if (enableRequestTiming)
{
    app.UseMiddleware<LegalAssistant.Logging.Middleware.RequestTimingMiddleware>();
}

app.MapPost("/embed", async (HttpContext http, EmbedRequest req, LegalAssistant.Embeddings.Services.IEmbeddingGenerator generator, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var correlationId = http.Request.Headers["X-Correlation-Id"].ToString();
    if (string.IsNullOrWhiteSpace(correlationId))
        correlationId = Guid.NewGuid().ToString("N");

    http.Response.Headers["X-Correlation-Id"] = correlationId;
    var logger = loggerFactory.CreateLogger("Embed");
    using var _ = logger.BeginScope(new System.Collections.Generic.Dictionary<string, object> { ["correlationId"] = correlationId });

    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest("Text is required");

    logger.LogInformation("Embedding request received");
    var vector = await generator.GenerateAsync(req.Text, ct);
    logger.LogInformation("Embedding response generated. Dimensions={Dimensions}", vector.Length);
    return Results.Ok(vector);
});

app.Run();

public sealed record EmbedRequest(string Text);
