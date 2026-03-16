using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LegalAssistant.Workers;
using System;
using LegalAssistant.Workers.Embeddings;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var conn = context.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(conn))
        {
            services.AddDbContext<LegalAssistantDbContext>(opt => opt.UseInMemoryDatabase("legal_dev"));
        }
        else
        {
            services.AddDbContext<LegalAssistantDbContext>(opt => opt.UseNpgsql(conn, o => o.UseVector()));
        }

        services.AddHttpClient();

        // Embedding service via RabbitMQ
        services.AddSingleton<IEmbeddingService, RabbitMqEmbeddingService>();
        services.AddHostedService<IngestWorker>();
        services.AddHostedService<RabbitMqConsumerService>();
    })
    .Build();

// Ensure database is created
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>();
    dbContext.Database.EnsureCreated();
}

await host.RunAsync();
