using System.Net;
using System.Net.Http.Headers;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.Documents;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Infrastructure.Chunking;
using LegalAssistant.Infrastructure.Chunks;
using LegalAssistant.Infrastructure.Documents;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Infrastructure.DependencyInjection;

public static class IngestInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIngestInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IngestJobProcessingOptions>>().Value);

        services.AddRabbitMqTopology<IngestRabbitMqTopology>();
        services.AddRabbitMqTopology<EmbeddingsRabbitMqTopology>();

        services.Configure<DocumentFetchOptions>(configuration.GetSection("Documents:Fetch"));
        services.AddSingleton<IHtmlToTextConverter, StructuredHtmlToTextConverter>();
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

        services.AddSingleton<IDocumentIngestJobPublisher, RabbitMqDocumentIngestJobPublisher>();
        services.AddScoped<IEmbeddingEnqueueService, EmbeddingRequestOutboxWriter>();
        services.AddEmbeddingStatusProcessing();

        services.AddSingleton<IChunkingStrategySelector, DefaultChunkingStrategySelector>();
        services.AddSingleton<IStrategyCandidate, ArticleCandidate>();
        services.AddSingleton<IStrategyCandidate, NumberedSectionCandidate>();
        services.AddSingleton<IStrategyCandidate, FixedSizeCandidate>();
        services.AddScoped<IChunkingRunRepository, EfChunkingRunRepository>();
        services.AddScoped<IChunkingRunService, ChunkingRunService>();

        // These repositories are required by the ingest processor and recovery,
        // and are also used by document/job command and query handlers.
        services.AddScoped<IDocumentRepository, EfDocumentRepository>();
        services.AddScoped<IJobRepository, EfJobRepository>();
        services.AddScoped<IDocumentChunkRepository, EfDocumentChunkRepository>();
        return services;
    }

    public static IServiceCollection AddIngestConsumers(this IServiceCollection services)
    {
        services.AddRabbitMqConsumer<EmbeddingCompletedMessage, RabbitMqEmbeddingCompletedConsumerDefinition>();
        services.AddRabbitMqConsumer<ReadOnlyMemory<byte>, RabbitMqIngestConsumerDefinition>();
        return services;
    }
}
