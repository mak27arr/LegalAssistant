using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RabbitMqConnectionOptions>();
        services.AddSingleton<IConfigureOptions<RabbitMqConnectionOptions>, RabbitMqConnectionOptionsSetup>();

        services.AddOptions<RabbitMqProcessingOptions>().Configure(options =>
        {
            var section = configuration.GetSection("RabbitMq:Processing");
            options.MaxAttempts = section.GetValue<int?>(nameof(RabbitMqProcessingOptions.MaxAttempts)) ?? options.MaxAttempts;
            options.InitialDelaySeconds = section.GetValue<int?>(nameof(RabbitMqProcessingOptions.InitialDelaySeconds)) ?? options.InitialDelaySeconds;
            options.MaxDelaySeconds = section.GetValue<int?>(nameof(RabbitMqProcessingOptions.MaxDelaySeconds)) ?? options.MaxDelaySeconds;
            options.BackoffMultiplier = section.GetValue<double?>(nameof(RabbitMqProcessingOptions.BackoffMultiplier)) ?? options.BackoffMultiplier;
        });

        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddSingleton<RabbitMqTopologyHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<RabbitMqTopologyHostedService>());

        return services;
    }

    public static IServiceCollection AddRabbitMqConsumer<TMessage, TDefinition>(
        this IServiceCollection services)
        where TDefinition : class, IRabbitMqConsumerDefinition<TMessage>
    {
        services.AddSingleton<IRabbitMqConsumerDefinition<TMessage>, TDefinition>();
        services.AddHostedService<RabbitMqConsumerHostedService<TMessage>>();
        return services;
    }

    public static IServiceCollection AddRabbitMqTopology<TDefinition>(
        this IServiceCollection services)
        where TDefinition : class, IRabbitMqTopologyDefinition
    {
        services.AddSingleton<IRabbitMqTopologyDefinition, TDefinition>();
        return services;
    }

}
