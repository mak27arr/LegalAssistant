using LegalAssistant.Application.Jobs.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Application.DependencyInjection;

public static class IngestApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIngestApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IIngestJobProcessor, IngestJobProcessor>();
        return services;
    }
}
