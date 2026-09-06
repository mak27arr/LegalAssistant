using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using LegalAssistant.Infrastructure.Chunking;
using LegalAssistant.Application.Ask.Services;
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
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Rag.Services;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Common;
using LegalAssistant.Application.Admin.Services;
using LegalAssistant.Core.Correlation;
using LegalAssistant.Infrastructure.Admin;
using LegalAssistant.Messaging;

namespace LegalAssistant.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    // Registers services implemented in the Infrastructure project.
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMqMessaging(configuration);
        services.AddOptions<IngestJobProcessingOptions>().Configure(options =>
        {
            var section = configuration.GetSection("RabbitMq:Processing");
            options.MaxAttempts = section.GetValue<int?>(nameof(IngestJobProcessingOptions.MaxAttempts)) ?? options.MaxAttempts;
            options.InitialDelaySeconds = section.GetValue<int?>(nameof(IngestJobProcessingOptions.InitialDelaySeconds)) ?? options.InitialDelaySeconds;
            options.MaxDelaySeconds = section.GetValue<int?>(nameof(IngestJobProcessingOptions.MaxDelaySeconds)) ?? options.MaxDelaySeconds;
            options.BackoffMultiplier = section.GetValue<double?>(nameof(IngestJobProcessingOptions.BackoffMultiplier)) ?? options.BackoffMultiplier;
            options.LeaseDurationSeconds = section.GetValue<int?>(nameof(IngestJobProcessingOptions.LeaseDurationSeconds)) ?? options.LeaseDurationSeconds;
            options.RecoveryIntervalSeconds = section.GetValue<int?>(nameof(IngestJobProcessingOptions.RecoveryIntervalSeconds)) ?? options.RecoveryIntervalSeconds;
            options.RecoveryBatchSize = section.GetValue<int?>(nameof(IngestJobProcessingOptions.RecoveryBatchSize)) ?? options.RecoveryBatchSize;
        });
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<IngestJobProcessingOptions>>().Value);
        services.AddRabbitMqTopology<IngestRabbitMqTopology>();
        services.AddRabbitMqTopology<EmbeddingsRabbitMqTopology>();
        services.AddRabbitMqTopology<LegalAssistant.Infrastructure.Ask.AskJobRabbitMqTopology>();

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
        services.AddSingleton<IHtmlToTextConverter, StructuredHtmlToTextConverter>();
        services.Configure<DocumentFetchOptions>(configuration.GetSection("Documents:Fetch"));
        services.AddSingleton<IDocumentUrlValidator, DocumentUrlValidator>();
        services.AddSingleton<IDocumentHtmlParser, ZakonRadaDocumentParser>();
        services.AddHttpClient<IDocumentContentFetcher, HttpDocumentContentFetcher>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; LegalAssistant/1.0; +https://local)");
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("uk-UA"));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("uk", 0.9));
                client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.8));
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            });

        // Ask embeddings/chunk search client implemented in Infrastructure
        services.AddScoped<IChunkSearchService, ChunkSearchService>();

        var embeddingsBase = configuration["Embeddings:BaseUrl"] ?? Environment.GetEnvironmentVariable("Embeddings__BaseUrl") ?? "http://embeddings";
        services.AddHttpClient<Application.Embeddings.IEmbeddingClient, HttpEmbeddingClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(embeddingsBase);
        });
        services.AddScoped<Application.Embeddings.IEmbeddingEnqueueService, RabbitMqEmbeddingRequestPublisher>();

        var ollamaBase = configuration["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("Ollama__BaseUrl") ?? "http://ollama:11434";
        services.AddHttpClient<LegalAssistant.Application.Rag.ILlmClient, LegalAssistant.Infrastructure.Rag.OllamaLlmClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(ollamaBase);
            var timeoutSeconds = configuration.GetValue<int?>("Ollama:TimeoutSeconds")
                                 ?? (int.TryParse(Environment.GetEnvironmentVariable("Ollama__TimeoutSeconds"), out var envTimeout)
                                     ? envTimeout
                                     : 300);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
        });

        services.AddSingleton<IDocumentIngestJobPublisher, RabbitMqDocumentIngestJobPublisher>();
        services.AddScoped<IMessageOutboxWriter, EfMessageOutboxWriter>();

        services.AddSingleton<IChunkingStrategySelector, DefaultChunkingStrategySelector>();
        services.AddSingleton<IStrategyCandidate, ArticleCandidate>();
        services.AddSingleton<IStrategyCandidate, NumberedSectionCandidate>();
        services.AddSingleton<IStrategyCandidate, FixedSizeCandidate>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAskJobRepository, EfAskJobRepository>();
        services.AddScoped<IAskJobEventRepository, EfAskJobEventRepository>();
        services.AddSingleton<IAskJobEventPublisher, RabbitMqAskJobEventPublisher>();
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IDocumentQueryService, DocumentQueryService>();
        services.AddScoped<IDocumentChunkQueryService, DocumentChunkQueryService>();
        services.AddScoped<IDocumentStatsQueryService, DocumentStatsQueryService>();
        services.AddScoped<IJobRepository, EfJobRepository>();
        services.AddScoped<IJobQueue, EfJobQueue>();
        services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();
        services.AddScoped<IAdminUserQueryService, AdminUserQueryService>();
        services.AddScoped<IAdminUserRoleService, AdminUserRoleService>();
        services.AddScoped<IAdminUserManagementService, AdminUserManagementService>();

        services.AddScoped<IChunkingRunRepository, EfChunkingRunRepository>();
        services.AddScoped<IChunkingRunService, ChunkingRunService>();

        services.AddSingleton<IRagPromptBuilder, DefaultRagPromptBuilder>();
        services.AddSingleton<IRagQueryPolicyProvider, ConfigurationRagQueryPolicyProvider>();
        services.AddScoped<IRagPromptTemplateProvider, DbRagPromptTemplateProvider>();

        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<LegalAssistant.Infrastructure.Auth.EfAuthSessionStore>();
        services.AddSingleton<LegalAssistant.Application.Auth.IUserSessionManager>(sp => sp.GetRequiredService<LegalAssistant.Infrastructure.Auth.EfAuthSessionStore>());
        services.AddSingleton<LegalAssistant.Infrastructure.Auth.IAuthSessionStore>(sp => sp.GetRequiredService<LegalAssistant.Infrastructure.Auth.EfAuthSessionStore>());
        services.AddSingleton<Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository, LegalAssistant.Infrastructure.Auth.EfDataProtectionKeyRepository>();

        return services;
    }

    public static IServiceCollection AddInfrastructureConsumers(this IServiceCollection services)
    {
        services.AddRabbitMqConsumer<EmbeddingCompletedMessage, RabbitMqEmbeddingCompletedConsumerDefinition>();
        services.AddRabbitMqConsumer<ReadOnlyMemory<byte>, RabbitMqIngestConsumerDefinition>();
        return services;
    }
}
