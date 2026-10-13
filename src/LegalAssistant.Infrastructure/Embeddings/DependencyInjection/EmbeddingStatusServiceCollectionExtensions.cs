using LegalAssistant.Application.Embeddings;
using LegalAssistant.Infrastructure.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LegalAssistant.Infrastructure.DependencyInjection;

public static class EmbeddingStatusServiceCollectionExtensions
{
    public static IServiceCollection AddEmbeddingStatusProcessing(this IServiceCollection services)
    {
        services.TryAddScoped<EmbeddingStatusService>();
        services.TryAddScoped<IEmbeddingStatusService, SerializableEmbeddingStatusService>();
        return services;
    }
}
