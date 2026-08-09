using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LegalAssistant.Infrastructure.Chunking;
using LegalAssistant.Application.Ask;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Infrastructure.Chunks;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Infrastructure.Rag;
using LegalAssistant.Infrastructure.Common;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Rag.Services;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Common;
using LegalAssistant.Core.Correlation;

namespace LegalAssistant.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    // Registers services implemented in the Infrastructure project.
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(conn))
        {
            services.AddDbContext<LegalAssistantDbContext>(opt => opt.UseInMemoryDatabase("legal_dev"));
        }
        else
        {
            services.AddDbContext<LegalAssistantDbContext>(opt => opt.UseNpgsql(conn, o => o.UseVector()));
        }

        services.AddHttpClient();

        // Ask embeddings/chunk search client implemented in Infrastructure
        services.AddScoped<IChunkSearchService, ChunkSearchService>();

        var embeddingsBase = configuration["Embeddings:BaseUrl"] ?? Environment.GetEnvironmentVariable("Embeddings__BaseUrl") ?? "http://embeddings";
        services.AddHttpClient<Application.Embeddings.IEmbeddingClient, HttpEmbeddingClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(embeddingsBase);
        });

        var ollamaBase = configuration["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("Ollama__BaseUrl") ?? "http://ollama:11434";
        services.AddHttpClient<LegalAssistant.Application.Rag.ILlmClient, LegalAssistant.Infrastructure.Rag.OllamaLlmClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(ollamaBase);
        });

        services.AddSingleton<IDocumentIngestJobPublisher, RabbitMqDocumentIngestJobPublisher>();

        services.AddSingleton<IChunkingStrategySelector, DefaultChunkingStrategySelector>();
        services.AddSingleton<IStrategyCandidate, ArticleCandidate>();
        services.AddSingleton<IStrategyCandidate, NumberedSectionCandidate>();
        services.AddSingleton<IStrategyCandidate, FixedSizeCandidate>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IJobRepository, EfJobRepository>();
        services.AddScoped<IJobQueue, EfJobQueue>();
        services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();

        services.AddScoped<IChunkingRunRepository, EfChunkingRunRepository>();
        services.AddScoped<IChunkingRunService, ChunkingRunService>();

        services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();
        services.AddScoped<IRagPromptTemplateProvider, DbRagPromptTemplateProvider>();

        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }

    public static IServiceCollection AddInfrastructureConsumers(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqEmbeddingCompletedConsumerHostedService>();
        services.AddHostedService<RabbitMqIngestConsumerHostedService>();
        return services;
    }
}
