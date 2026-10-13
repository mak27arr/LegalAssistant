using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Messaging.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LegalAssistant.WorkerIngest.DependencyInjection;

public static class IngestWorkerServiceCollectionExtensions
{
    public static IServiceCollection AddIngestWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(30));
        services.AddIngestApplicationServices();
        services.AddSharedInfrastructure(configuration);
        services.AddIngestInfrastructure(configuration);
        services.AddIngestConsumers();

        services.AddScoped<IOutboxMessagePublisher, DocumentIngestOutboxPublisher>();
        services.AddScoped<IOutboxMessagePublisher, EmbeddingRequestOutboxPublisher>();
        services.AddScoped<IOutboxMaintenance, DocumentIngestOutboxRepairer>();
        services.AddOutboxDispatcher(configuration, OutboxMessageOwners.Ingest);
        services.AddHostedService<StaleIngestJobRecoveryHostedService>();
        return services;
    }
}
