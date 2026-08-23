using LegalAssistant.Core.Correlation;
using LegalAssistant.Api.Services;
using LegalAssistant.Api.Common;
using LegalAssistant.Infrastructure.Ask;

namespace LegalAssistant.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, ApiCorrelationContext>();
        services.AddSingleton<LegalAssistant.Application.Ask.IAskJobEventFanout, InMemoryAskJobEventFanout>();
        services.AddScoped<IAskJobEventStreamService, AskJobEventStreamService>();
        services.AddHostedService<RabbitMqAskJobEventRelayHostedService>();

        return services;
    }
}
