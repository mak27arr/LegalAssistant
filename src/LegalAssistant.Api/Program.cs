using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using LegalAssistant.Api.Messaging;
using Microsoft.Extensions.Configuration;
using System;
using LegalAssistant.Api.Services;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Chunks;

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

// Messaging - use RabbitMQ publisher
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

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

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddScoped<IJobRepository, EfJobRepository>();
builder.Services.AddScoped<IJobQueue, EfJobQueue>();
builder.Services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();

// Application services
builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

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
