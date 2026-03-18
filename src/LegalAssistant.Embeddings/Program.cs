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
    return new LegalAssistant.Embeddings.Services.OllamaEmbeddingGenerator(http, model);
});

builder.Services.AddHostedService<LegalAssistant.Embeddings.Messaging.EmbeddingQueueWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
