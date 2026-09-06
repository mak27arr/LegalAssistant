using LegalAssistant.Logging.DependencyInjection;
using LegalAssistant.Infrastructure.Health;
using LegalAssistant.Embeddings.ServiceEndpoints;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Messaging;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddRabbitMqTopology<EmbeddingsRabbitMqTopology>();

builder.Services.AddHttpClient("ollama", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://ollama:11434";
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

builder.Services.AddRabbitMqConsumer<
    LegalAssistant.Embeddings.Messaging.EmbeddingRequestMessage,
    LegalAssistant.Embeddings.Messaging.EmbeddingRequestConsumerDefinition>();
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
