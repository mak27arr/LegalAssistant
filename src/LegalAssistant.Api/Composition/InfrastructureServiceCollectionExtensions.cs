using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LegalAssistant.Infrastructure.DependencyInjection;

namespace LegalAssistant.Api.Composition;

public static class InfrastructureServiceCollectionExtensions
{
    // Compatibility composition for the API. Fixed-purpose workers must call
    // only the focused modules for their role.
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSharedInfrastructure(configuration);
        services.AddIngestInfrastructure(configuration);
        services.AddAskInfrastructure(configuration);
        services.AddApiInfrastructureSupport();
        return services;
    }
}
