using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using LegalAssistant.Application.Messaging;

namespace LegalAssistant.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcherOptions
{
    public OutboxDispatcherOptions(
        IEnumerable<string> owners,
        TimeSpan? pollInterval = null,
        TimeSpan? leaseDuration = null,
        int batchSize = 25,
        int maxAttempts = 10)
    {
        Owners = owners.Distinct(StringComparer.Ordinal).ToArray();
        MessageTypes = OutboxMessageOwners.MessageTypesForOwners(Owners);
        PollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        LeaseDuration = leaseDuration ?? TimeSpan.FromMinutes(2);
        BatchSize = Math.Clamp(batchSize, 1, 200);
        MaxAttempts = Math.Max(1, maxAttempts);
    }

    public IReadOnlyCollection<string> Owners { get; }

    public IReadOnlyCollection<string> MessageTypes { get; }
    public TimeSpan PollInterval { get; }
    public TimeSpan LeaseDuration { get; }
    public int BatchSize { get; }
    public int MaxAttempts { get; }
}

public static class OutboxDispatcherServiceCollectionExtensions
{
    public static IServiceCollection AddOutboxDispatcher(
        this IServiceCollection services,
        params string[] owners)
        => services.AddOutboxDispatcher(null, owners);

    public static IServiceCollection AddOutboxDispatcher(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration? configuration,
        params string[] owners)
    {
        if (owners.Length == 0)
            throw new ArgumentException("At least one outbox owner is required.", nameof(owners));

        var section = configuration?.GetSection("Outbox:Processing");
        var pollSeconds = section?.GetValue<int?>("PollIntervalSeconds") ?? 5;
        var leaseSeconds = section?.GetValue<int?>("LeaseDurationSeconds") ?? 120;
        var batchSize = section?.GetValue<int?>("BatchSize") ?? 25;
        var maxAttempts = section?.GetValue<int?>("MaxAttempts") ?? 10;
        services.AddSingleton(new OutboxDispatcherOptions(
            owners,
            TimeSpan.FromSeconds(Math.Max(1, pollSeconds)),
            TimeSpan.FromSeconds(Math.Max(5, leaseSeconds)),
            batchSize,
            maxAttempts));
        services.AddHostedService<OutboxDispatcherHostedService>();
        return services;
    }
}
