using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Messaging;

namespace LegalAssistant.Workers.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructureServices(configuration);

        services.AddScoped<IIngestJobProcessor, IngestJobProcessor>();

        services.AddInfrastructureConsumers();
        services.AddHostedService<QueuedJobOutboxDispatcherHostedService>();
        services.AddHostedService<AskJobOutboxDispatcherHostedService>();
        services.AddHostedService<AskJobWorkerHostedService>();

        return services;
    }
}
