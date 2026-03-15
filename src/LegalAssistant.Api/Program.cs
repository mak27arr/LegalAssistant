using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using LegalAssistant.Api.Messaging;
using Microsoft.Extensions.Configuration;
using System;
using LegalAssistant.Api.Services;

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
    builder.Services.AddDbContext<LegalAssistantDbContext>(opt => opt.UseNpgsql(conn));
}

// Messaging - use RabbitMQ publisher
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

// Hosted services
// The ingest worker runs in the separate worker service; do not register it in the API.
// MessagePollingService is not needed when using RabbitMQ

// HttpClient for workers that need to fetch remote documents
builder.Services.AddHttpClient();

// Application services
builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
    dbContext.Database.EnsureCreated();
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
