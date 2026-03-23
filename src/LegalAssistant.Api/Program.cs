using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Services;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Infrastructure.Chunking;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Rag;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Chunks;
using LegalAssistant.Infrastructure.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext - use in-memory for initial dev if no connection string provided
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(conn))
{
    builder.Services.AddDbContext<LegalAssistantDbContext>(opt => opt.UseInMemoryDatabase("legal_dev"));
}
else
{
    builder.Services.AddDbContext<LegalAssistantDbContext>(opt => opt.UseNpgsql(conn, o => o.UseVector()));
}

// Messaging is implemented in Infrastructure
builder.Services.AddSingleton<IDocumentIngestJobPublisher, RabbitMqDocumentIngestJobPublisher>();

// Hosted services
// The ingest worker runs in the separate worker service; do not register it in the API.
// MessagePollingService is not needed when using RabbitMQ

// HttpClient for workers that need to fetch remote documents
builder.Services.AddHttpClient();

builder.Services.AddHttpClient<IEmbeddingClient, HttpEmbeddingClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Embeddings:BaseUrl"] ?? Environment.GetEnvironmentVariable("Embeddings__BaseUrl") ?? "http://embeddings";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddScoped<IAskService, AskService>();
builder.Services.AddScoped<IChunkSearchService, ChunkSearchService>();

builder.Services.AddScoped<IRagAnswerService, RagAnswerService>();
builder.Services.AddScoped<IRagPromptTemplateProvider, DbRagPromptTemplateProvider>();
builder.Services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();
builder.Services.AddHttpClient<ILlmClient, OllamaLlmClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("Ollama__BaseUrl") ?? "http://ollama:11434";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddScoped<IJobRepository, EfJobRepository>();
builder.Services.AddScoped<IJobQueue, EfJobQueue>();
builder.Services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();

builder.Services.AddScoped<IJobQueryService, JobQueryService>();

builder.Services.AddSingleton<IChunkingStrategySelector, DefaultChunkingStrategySelector>();
builder.Services.AddSingleton<IDocumentChunkingPolicyFactory, DefaultDocumentChunkingPolicyFactory>();
builder.Services.AddScoped<IChunkingRunRepository, EfChunkingRunRepository>();
builder.Services.AddScoped<IChunkingRunService, ChunkingRunService>();

builder.Services.AddScoped<ICorrelationContext, CorrelationContext>();
builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddScoped<IDocumentCommandService, DocumentCommandService>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Correlation-Id"] = correlationId;
    }

    context.Response.Headers["X-Correlation-Id"] = correlationId.ToString();

    var corr = context.RequestServices.GetRequiredService<ICorrelationContext>();
    corr.CorrelationId = correlationId.ToString();

    var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("Correlation");
    using (logger.BeginScope(new System.Collections.Generic.Dictionary<string, object> { ["correlationId"] = corr.CorrelationId }))
    {
        await next();
    }
});

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
    dbContext.Database.Migrate();
}

// Configure URLs
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrEmpty(urls))
{
    app.Urls.Add("http://0.0.0.0:80");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();

app.Run();
