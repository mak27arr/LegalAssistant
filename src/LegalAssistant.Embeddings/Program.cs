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

builder.Services.AddSingleton<LegalAssistant.Embeddings.Services.IEmbeddingGenerator, LegalAssistant.Embeddings.Services.DeterministicMockEmbeddingGenerator>();
builder.Services.AddHostedService<LegalAssistant.Embeddings.Messaging.EmbeddingQueueWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
