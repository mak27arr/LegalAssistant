using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Ask;
using LegalAssistant.Application.Chunks;
using LegalAssistant.Application.DependencyInjection;
using LegalAssistant.Application.Documents.Services;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Application.Jobs.Services;
using LegalAssistant.Application.Rag;
using LegalAssistant.Api.Controllers;
using LegalAssistant.Infrastructure.Ask;
using LegalAssistant.Infrastructure.Jobs;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Infrastructure.Messaging.Outbox;
using LegalAssistant.Messaging;
using LegalAssistant.WorkerAsk.DependencyInjection;
using LegalAssistant.WorkerIngest.DependencyInjection;
using LegalAssistant.Api.DependencyInjection;
using LegalAssistant.Api.Composition;
using LegalAssistant.Infrastructure.DependencyInjection;
using LegalAssistant.Infrastructure.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LegalAssistant.BackendTests.Workers;

public sealed class WorkerRoleRegistrationTests
{
    [Fact]
    public void IngestRole_RegistersOnlyIngestPublishersAndHostedWork()
    {
        var services = CreateServices();
        services.AddIngestWorkerServices(new ConfigurationBuilder().Build());

        var options = GetDispatcherOptions(services);
        Assert.Equal([OutboxMessageOwners.Ingest], options.Owners);
        Assert.Contains(typeof(StaleIngestJobRecoveryHostedService), HostedServiceTypes(services));
        Assert.DoesNotContain(typeof(AskJobWorkerHostedService), HostedServiceTypes(services));
        Assert.Contains(typeof(DocumentIngestOutboxPublisher), PublisherTypes(services));
        Assert.Contains(typeof(EmbeddingRequestOutboxPublisher), PublisherTypes(services));
        Assert.DoesNotContain(typeof(AskJobEventOutboxPublisher), PublisherTypes(services));
        Assert.Contains(typeof(IIngestJobProcessor), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IAskJobProcessor), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IAskJobService), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IDocumentCommandService), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IEmbeddingClient), ServiceTypes(services));
        Assert.Contains(typeof(IngestRabbitMqTopology), TopologyTypes(services));
        Assert.Contains(typeof(EmbeddingsRabbitMqTopology), TopologyTypes(services));
        Assert.DoesNotContain(typeof(AskJobRabbitMqTopology), TopologyTypes(services));
        Assert.Contains(typeof(RabbitMqIngestConsumerDefinition), ConsumerDefinitionTypes(services));
        Assert.Contains(typeof(RabbitMqEmbeddingCompletedConsumerDefinition), ConsumerDefinitionTypes(services));
    }

    [Fact]
    public void AskRole_RegistersOnlyAskPublisherAndHostedWork()
    {
        var services = CreateServices();
        services.AddAskWorkerServices(new ConfigurationBuilder().Build());

        var options = GetDispatcherOptions(services);
        Assert.Equal([OutboxMessageOwners.Ask], options.Owners);
        Assert.Contains(typeof(AskJobWorkerHostedService), HostedServiceTypes(services));
        Assert.DoesNotContain(typeof(StaleIngestJobRecoveryHostedService), HostedServiceTypes(services));
        Assert.Contains(typeof(AskJobEventOutboxPublisher), PublisherTypes(services));
        Assert.DoesNotContain(typeof(DocumentIngestOutboxPublisher), PublisherTypes(services));
        Assert.DoesNotContain(typeof(EmbeddingRequestOutboxPublisher), PublisherTypes(services));
        Assert.Contains(typeof(AskJobRabbitMqTopology), TopologyTypes(services));
        Assert.DoesNotContain(typeof(IngestRabbitMqTopology), TopologyTypes(services));
        Assert.DoesNotContain(typeof(EmbeddingsRabbitMqTopology), TopologyTypes(services));
        Assert.DoesNotContain(typeof(RabbitMqIngestConsumerDefinition), ConsumerDefinitionTypes(services));
        Assert.DoesNotContain(typeof(RabbitMqEmbeddingCompletedConsumerDefinition), ConsumerDefinitionTypes(services));
        Assert.Contains(typeof(IAskJobProcessor), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IIngestJobProcessor), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IAskJobService), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IDocumentCommandService), ServiceTypes(services));
        Assert.DoesNotContain(typeof(IEmbeddingStatusService), ServiceTypes(services));

    }

    [Fact]
    public void EachWorkerRole_BuildsItsProcessorFromFocusedComposition()
    {
        using var ingestProvider = BuildProvider(services =>
            services.AddIngestWorkerServices(new ConfigurationBuilder().Build()));
        using var ingestScope = ingestProvider.CreateScope();
        Assert.NotNull(ingestScope.ServiceProvider.GetRequiredService<IIngestJobProcessor>());

        using var askProvider = BuildProvider(services =>
            services.AddAskWorkerServices(new ConfigurationBuilder().Build()));
        using var askScope = askProvider.CreateScope();
        Assert.NotNull(askScope.ServiceProvider.GetRequiredService<IAskJobProcessor>());
    }

    [Fact]
    public void ApiComposition_ResolvesApiCommandsWithoutActivatingWorkerConsumers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "tests",
                ["Authentication:Jwt:Audience"] = "tests",
                ["Authentication:Jwt:SigningKey"] = "test-signing-key-long-enough-for-validation-only"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructureServices(configuration);
        services.AddApiApplicationServices();
        services.AddApiInfrastructure(configuration, new TestHostEnvironment());

        Assert.DoesNotContain(typeof(RabbitMqIngestConsumerDefinition), ConsumerDefinitionTypes(services));
        Assert.DoesNotContain(typeof(RabbitMqEmbeddingCompletedConsumerDefinition), ConsumerDefinitionTypes(services));
        Assert.DoesNotContain(typeof(AskJobWorkerHostedService), HostedServiceTypes(services));
        Assert.DoesNotContain(typeof(StaleIngestJobRecoveryHostedService), HostedServiceTypes(services));
        Assert.DoesNotContain(typeof(OutboxDispatcherHostedService), HostedServiceTypes(services));
        Assert.Contains(typeof(RabbitMqAskJobEventRelayConsumerDefinition), ConsumerDefinitionTypes(services));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDocumentCommandService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDocumentChunkQueryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDocumentQueryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDocumentStatsQueryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRagAnswerService>());
        Assert.NotNull(ActivatorUtilities.CreateInstance<DocumentsController>(scope.ServiceProvider));
        Assert.NotNull(ActivatorUtilities.CreateInstance<AskController>(scope.ServiceProvider));
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<LegalAssistantDbContext>());
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();
        return services;
    }

    private static OutboxDispatcherOptions GetDispatcherOptions(ServiceCollection services)
        => (OutboxDispatcherOptions)services.Single(x => x.ServiceType == typeof(OutboxDispatcherOptions)).ImplementationInstance!;

    private static IReadOnlyCollection<Type> PublisherTypes(ServiceCollection services)
        => services
            .Where(x => x.ServiceType == typeof(IOutboxMessagePublisher))
            .Select(x => x.ImplementationType!)
            .ToArray();

    private static IReadOnlyCollection<Type> ServiceTypes(ServiceCollection services)
        => services.Select(x => x.ServiceType).ToArray();

    private static ServiceProvider BuildProvider(Action<ServiceCollection> configure)
    {
        var services = CreateServices();
        configure(services);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static IReadOnlyCollection<Type> HostedServiceTypes(ServiceCollection services)
        => services
            .Where(x => x.ServiceType == typeof(IHostedService))
            .Select(x => x.ImplementationType!)
            .ToArray();

    private static IReadOnlyCollection<Type> TopologyTypes(ServiceCollection services)
        => services
            .Where(x => x.ServiceType == typeof(IRabbitMqTopologyDefinition))
            .Select(x => x.ImplementationType!)
            .ToArray();

    private static IReadOnlyCollection<Type> ConsumerDefinitionTypes(ServiceCollection services)
        => services
            .Where(x => x.ServiceType.IsGenericType &&
                        x.ServiceType.GetGenericTypeDefinition() == typeof(IRabbitMqConsumerDefinition<>))
            .Select(x => x.ImplementationType!)
            .ToArray();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LegalAssistant.BackendTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
