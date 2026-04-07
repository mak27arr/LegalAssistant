using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Infrastructure.Chunking;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Infrastructure.Common;
using LegalAssistant.Application.Rag.Services;
using LegalAssistant.Application.Common;

namespace LegalAssistant.Workers.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
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
        services.AddSingleton<IDocumentIngestJobPublisher, RabbitMqDocumentIngestJobPublisher>();

        services.AddScoped<IIngestJobProcessor, IngestJobProcessor>();

        services.AddScoped<IChunkingRunRepository, EfChunkingRunRepository>();
        services.AddScoped<IChunkingRunService, ChunkingRunService>();

        services.AddScoped<IJobQueryService, LegalAssistant.Application.Jobs.Services.JobQueryService>();
        services.AddScoped<IDocumentCommandService, LegalAssistant.Application.Documents.Services.DocumentCommandService>();

        services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();
        services.AddScoped<ICorrelationContext, CorrelationContext>();

        services.AddHostedService<RabbitMqEmbeddingCompletedConsumerHostedService>();
        services.AddHostedService<RabbitMqIngestConsumerHostedService>();

        return services;
    }
}
