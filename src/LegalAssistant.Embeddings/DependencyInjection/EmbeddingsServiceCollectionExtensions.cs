using LegalAssistant.Application.Common;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Embeddings.Messaging;
using LegalAssistant.Embeddings.Services;
using LegalAssistant.Infrastructure.Common;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Embeddings;
using LegalAssistant.Infrastructure.Health;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Logging.DependencyInjection;
using LegalAssistant.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Embeddings.DependencyInjection;

public static class EmbeddingsServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddingsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRabbitMqMessaging(configuration);
        services.AddRabbitMqTopology<EmbeddingsRabbitMqTopology>();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<LegalAssistantDbContext>(options =>
                options.UseNpgsql(connectionString, postgres => postgres.UseVector()));
        }
        else
        {
            services.AddDbContext<LegalAssistantDbContext>(options =>
                options.UseInMemoryDatabase("legal_dev"));
        }

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IEmbeddingStatusService, EmbeddingStatusService>();

        services.AddHttpClient("ollama", (serviceProvider, client) =>
        {
            var baseUrl = configuration["Ollama:BaseUrl"]
                          ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
                          ?? "http://ollama:11434";
            client.BaseAddress = new Uri(baseUrl);
        });

        services.AddSingleton<IEmbeddingGenerator>(serviceProvider =>
        {
            var mode = (configuration["Embeddings:Mode"] ?? "ollama")
                .Trim()
                .ToLowerInvariant();

            if (mode == "mock")
                return new DeterministicMockEmbeddingGenerator();

            var model = configuration["Ollama:Model"]
                        ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL")
                        ?? "nomic-embed-text";
            var http = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
            var logger = serviceProvider.GetRequiredService<ILogger<OllamaEmbeddingGenerator>>();
            return new OllamaEmbeddingGenerator(http, model, logger);
        });

        services.AddRabbitMqConsumer<EmbeddingRequestMessage, EmbeddingRequestConsumerDefinition>();
        services.AddEmbeddingsReadinessHealthChecks(configuration);
        services.AddCentralizedLogging(configuration, "embeddings");

        return services;
    }
}
