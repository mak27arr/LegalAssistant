using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Messaging.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LegalAssistant.WorkerAsk.DependencyInjection;

public static class AskWorkerServiceCollectionExtensions
{
    public static IServiceCollection AddAskWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(30));
        services.AddAskApplicationServices();
        services.AddSharedInfrastructure(configuration);
        services.AddAskInfrastructure(configuration);

        services.AddScoped<IOutboxMessagePublisher, AskJobEventOutboxPublisher>();
        services.AddOutboxDispatcher(configuration, OutboxMessageOwners.Ask);
        services.AddHostedService<AskJobWorkerHostedService>();
        services.AddHostedService<AskJobRecoveryHostedService>();
        return services;
    }
}
