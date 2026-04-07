using Microsoft.Extensions.DependencyInjection;
using LegalAssistant.Application.Chunking.Services;
using LegalAssistant.Infrastructure.Chunking;

namespace LegalAssistant.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChunkingServices(this IServiceCollection services)
    {
        services.AddSingleton<IChunkingStrategySelector, DefaultChunkingStrategySelector>();
        services.AddSingleton<IStrategyCandidate, ArticleCandidate>();
        services.AddSingleton<IStrategyCandidate, NumberedSectionCandidate>();
        services.AddSingleton<IStrategyCandidate, FixedSizeCandidate>();

        return services;
    }
}
