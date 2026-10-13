using LegalAssistant.Api.Health;
using LegalAssistant.Infrastructure.Health;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Api.Composition;

public static class ApiHealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddApiReadinessHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresConnectionHealthCheck>("postgres", tags: new[] { "ready" })
            .AddCheck<ApiEmbeddingsReadinessHealthCheck>("embeddings", tags: new[] { "ready" })
            .AddCheck<ApiOllamaReadinessHealthCheck>("ollama", tags: new[] { "ready" });

        return services;
    }
}
