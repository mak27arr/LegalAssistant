using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LegalAssistant.Application.Common;
using LegalAssistant.Logging.Middleware;
using LegalAssistant.Logging.Processing;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace LegalAssistant.Logging.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCentralizedLogging(this IServiceCollection services, IConfiguration configuration, string? serviceName = null)
    {
        services.AddSingleton<IProcessingTimer, ProcessingTimer>();

        // Determine service name: parameter, configuration key, or environment variable
        var resolvedServiceName = serviceName
            ?? configuration["ServiceName"]
            ?? Environment.GetEnvironmentVariable("SERVICE_NAME");

        var defaultFile = resolvedServiceName is null ? "logs/app.log" : $"logs/{resolvedServiceName}.log";
        var path = Environment.GetEnvironmentVariable("LOG_PATH")
                   ?? configuration["Logging:Path"]
                   ?? defaultFile;

        // Configure Serilog for structured JSON logging to file (Sidecar pattern)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", resolvedServiceName ?? "Unknown")
            .WriteTo.File(new RenderedCompactJsonFormatter(), path, 
                rollingInterval: RollingInterval.Day,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(Log.Logger, dispose: true);
        });

        return services;
    }
}

