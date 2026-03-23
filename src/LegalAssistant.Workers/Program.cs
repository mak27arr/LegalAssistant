using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Chunks;
using LegalAssistant.Application.Common;
using LegalAssistant.Infrastructure.Common;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Rag.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Infrastructure.Chunking;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLogging(logging =>
        {
            logging.AddFilter((category, level) =>
                category == "Microsoft.EntityFrameworkCore.Database.Command" ? level >= LogLevel.Warning : true);
        });

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

        services.AddSingleton<IChunkingStrategySelector, DefaultChunkingStrategySelector>();
        services.AddSingleton<IDocumentChunkingPolicyFactory, DefaultDocumentChunkingPolicyFactory>();
        services.AddScoped<IChunkingRunRepository, EfChunkingRunRepository>();
        services.AddScoped<IChunkingRunService, ChunkingRunService>();

        // Embedding service via RabbitMQ
        services.AddSingleton<IEmbeddingEnqueueService, LegalAssistant.Infrastructure.Messaging.RabbitMqEmbeddingRequestPublisher>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IJobRepository, EfJobRepository>();
        services.AddScoped<IJobQueue, EfJobQueue>();
        services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();

        services.AddScoped<IIngestJobProcessor, IngestJobProcessor>();

        services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();

        services.AddScoped<ICorrelationContext, CorrelationContext>();

        services.AddHostedService<LegalAssistant.Infrastructure.Messaging.RabbitMqEmbeddingCompletedConsumerHostedService>();
        services.AddHostedService<LegalAssistant.Infrastructure.Messaging.RabbitMqIngestConsumerHostedService>();
    })
    .Build();

// Database migrations are applied by the API on startup.

await host.RunAsync();
