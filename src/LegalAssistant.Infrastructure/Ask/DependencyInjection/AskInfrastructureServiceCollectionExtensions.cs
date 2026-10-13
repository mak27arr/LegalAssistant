using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Rag;
using LegalAssistant.Application.Rag.Services;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Infrastructure.Rag;
using LegalAssistant.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Infrastructure.DependencyInjection;

public static class AskInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAskInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AskJobProcessingOptions>().Configure(options =>
        {
            var section = configuration.GetSection("Ask:Processing");
            options.PollIntervalSeconds = section.GetValue<int?>(nameof(AskJobProcessingOptions.PollIntervalSeconds)) ?? options.PollIntervalSeconds;
            options.LeaseDurationSeconds = section.GetValue<int?>(nameof(AskJobProcessingOptions.LeaseDurationSeconds)) ?? options.LeaseDurationSeconds;
            options.RecoveryIntervalSeconds = section.GetValue<int?>(nameof(AskJobProcessingOptions.RecoveryIntervalSeconds)) ?? options.RecoveryIntervalSeconds;
            options.RecoveryBatchSize = section.GetValue<int?>(nameof(AskJobProcessingOptions.RecoveryBatchSize)) ?? options.RecoveryBatchSize;
        });
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AskJobProcessingOptions>>().Value);

        services.AddRabbitMqTopology<AskJobRabbitMqTopology>();

        services.AddScoped<IChunkSearchService, ChunkSearchService>();
        services.AddHttpClient<IEmbeddingClient, HttpEmbeddingClient>((_, client) =>
        {
            var embeddingsBase = configuration["Embeddings:BaseUrl"]
                                 ?? Environment.GetEnvironmentVariable("Embeddings__BaseUrl")
                                 ?? "http://embeddings";
            client.BaseAddress = new Uri(embeddingsBase);
        });

        services.AddHttpClient<ILlmClient, OllamaLlmClient>((_, client) =>
        {
            var ollamaBase = configuration["Ollama:BaseUrl"]
                             ?? Environment.GetEnvironmentVariable("Ollama__BaseUrl")
                             ?? "http://ollama:11434";
            client.BaseAddress = new Uri(ollamaBase);
            var timeoutSeconds = configuration.GetValue<int?>("Ollama:TimeoutSeconds")
                                 ?? (int.TryParse(Environment.GetEnvironmentVariable("Ollama__TimeoutSeconds"), out var envTimeout)
                                     ? envTimeout
                                     : 300);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
        });

        services.AddScoped<IAskJobRepository, EfAskJobRepository>();
        services.AddScoped<IAskJobEventRepository, EfAskJobEventRepository>();
        services.AddSingleton<IAskJobEventPublisher, RabbitMqAskJobEventPublisher>();
        services.AddSingleton<IRagQueryPolicyProvider, ConfigurationRagQueryPolicyProvider>();
        services.AddScoped<IRagPromptTemplateProvider, DbRagPromptTemplateProvider>();
        return services;
    }
}
