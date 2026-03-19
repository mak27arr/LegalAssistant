using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LegalAssistant.Workers;
using System;
using LegalAssistant.Workers.Embeddings;
using LegalAssistant.Domain.Chunking;
using System.Text.RegularExpressions;
using LegalAssistant.Domain.Documents;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Chunks;

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

        services.AddSingleton<IHtmlToTextConverter, RegexHtmlToTextConverter>();
        services.AddHttpClient<IDocumentContentFetcher, HttpDocumentContentFetcher>();

        services.AddSingleton<IChunkingPolicy>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var chunkSize = config.GetValue<int?>("Chunking:ChunkSize") ?? 2000;
            var maxChunkSize = config.GetValue<int?>("Chunking:MaxChunkSize") ?? chunkSize;
            var pattern = config.GetValue<string>("Chunking:ArticleRegex") ?? @"Стаття\s+\d+[\d¹²³]*[\w\-]*";

            var articleRegex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            var regex = new RegexArticleChunkingStrategy(articleRegex, maxChunkSize: maxChunkSize);
            var fallback = new FixedSizeChunkingStrategy(chunkSize: chunkSize);
            return new RegexOrFixedChunkingPolicy(regex, fallback);
        });

        // Embedding service via RabbitMQ
        services.AddSingleton<IEmbeddingService, RabbitMqEmbeddingService>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IJobRepository, EfJobRepository>();
        services.AddScoped<IJobQueue, EfJobQueue>();
        services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();

        services.AddHostedService<IngestWorker>();
        services.AddHostedService<EmbeddingCompletedConsumer>();
        services.AddHostedService<RabbitMqConsumerService>();
    })
    .Build();

// Database migrations are applied by the API on startup.

await host.RunAsync();
