using LegalAssistant.Logging.DependencyInjection;
using LegalAssistant.Infrastructure.Health;
using LegalAssistant.Embeddings.ServiceEndpoints;
var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddEmbeddingsReadinessHealthChecks(builder.Configuration);

builder.Services.AddCentralizedLogging(builder.Configuration, "embeddings");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var enableRequestTiming = builder.Configuration.GetValue<bool?>("Logging:RequestTiming:Enabled") ?? true;
if (enableRequestTiming)
{
    app.UseMiddleware<LegalAssistant.Logging.Middleware.RequestTimingMiddleware>();
}

app.MapEmbedEndpoint();
app.MapHealthEndpoint();

app.Run();
