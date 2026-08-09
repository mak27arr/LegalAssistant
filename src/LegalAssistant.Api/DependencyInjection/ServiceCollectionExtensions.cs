using LegalAssistant.Core.Correlation;
using LegalAssistant.Api.Common;

namespace LegalAssistant.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, ApiCorrelationContext>();

        return services;
    }
}
