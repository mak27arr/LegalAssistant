using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LegalAssistant.Embeddings.ServiceEndpoints;

public static class HealthEndpoint
{
    public static WebApplication MapHealthEndpoint(this WebApplication app)
    {
        var readyOptions = new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        };

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", readyOptions);
        app.MapHealthChecks("/health", readyOptions);

        return app;
    }
}
