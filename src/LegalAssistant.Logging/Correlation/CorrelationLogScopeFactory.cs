using LegalAssistant.Core.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Logging.Correlation;

public static class CorrelationLogScopeFactory
{
    public static CorrelationLogScope Create(
        IServiceProvider services,
        ILogger logger,
        string? correlationId,
        string source,
        IReadOnlyDictionary<string, object?>? additionalProperties = null)
    {
        var resolvedCorrelationId = correlationId;
        if (string.IsNullOrWhiteSpace(resolvedCorrelationId))
        {
            resolvedCorrelationId = Guid.NewGuid().ToString("N");
            logger.LogWarning(
                "Missing correlation id in {Source}. Generated fallback correlationId={CorrelationId}",
                source,
                resolvedCorrelationId);
        }

        var correlationContext = services.GetRequiredService<ICorrelationContext>();
        correlationContext.CorrelationId = resolvedCorrelationId;

        var scopeData = new Dictionary<string, object?>
        {
            ["correlationId"] = resolvedCorrelationId
        };

        if (additionalProperties is not null)
        {
            foreach (var pair in additionalProperties)
                scopeData[pair.Key] = pair.Value;
        }

        var scope = logger.BeginScope(scopeData);
        return new CorrelationLogScope(resolvedCorrelationId, scope);
    }
}
