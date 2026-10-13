using Microsoft.Extensions.DependencyInjection;
using LegalAssistant.Application.DependencyInjection;

namespace LegalAssistant.Api.Composition;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddIngestApplicationServices();
        services.AddAskApplicationServices();
        services.AddApiApplicationServices();
        return services;
    }
}
