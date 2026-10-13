using LegalAssistant.Application.Common;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Application.Persistence;
using LegalAssistant.Core.Correlation;
using LegalAssistant.Infrastructure.Common;
using LegalAssistant.Infrastructure.Db;
using LegalAssistant.Infrastructure.Messaging.Outbox;
using LegalAssistant.Infrastructure.Messaging;
using LegalAssistant.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAssistant.Infrastructure.DependencyInjection;

public static class SharedInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSharedMessaging(configuration);
        services.AddSharedPersistence(configuration);
        return services;
    }

    public static IServiceCollection AddSharedMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRabbitMqMessaging(configuration);
        services.AddOutboxInfrastructure(configuration);
        services.AddHttpClient();
        return services;
    }

    public static IServiceCollection AddSharedPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<LegalAssistantDbContext>(options =>
                options.UseInMemoryDatabase("legal_dev"));
        }
        else
        {
            services.AddDbContext<LegalAssistantDbContext>(options =>
                options.UseNpgsql(connectionString, postgres => postgres.UseVector()));
        }

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }

    public static IServiceCollection AddOutboxInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddSingleton<OutboxDispatcherMetrics>();
        services.AddSingleton<IOutboxNotificationListener>(serviceProvider =>
            new PostgresOutboxNotificationListener(
                connectionString,
                serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PostgresOutboxNotificationListener>>()));
        services.AddScoped<IMessageOutboxWriter, EfMessageOutboxWriter>();
        return services;
    }
}
