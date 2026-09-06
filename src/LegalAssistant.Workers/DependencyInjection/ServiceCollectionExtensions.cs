using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Messaging.Outbox;

namespace LegalAssistant.Workers.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddInfrastructureServices(configuration);

        services.AddScoped<IIngestJobProcessor, IngestJobProcessor>();

        services.AddInfrastructureConsumers();
        services.AddScoped<IOutboxMessagePublisher, DocumentIngestOutboxPublisher>();
        services.AddScoped<IOutboxMessagePublisher, EmbeddingRequestOutboxPublisher>();
        services.AddScoped<IOutboxMessagePublisher, AskJobEventOutboxPublisher>();
        services.AddScoped<IOutboxMaintenance, DocumentIngestOutboxRepairer>();
        services.AddHostedService<OutboxDispatcherHostedService>();
        services.AddHostedService<StaleIngestJobRecoveryHostedService>();
        services.AddHostedService<AskJobWorkerHostedService>();

        return services;
    }
}
