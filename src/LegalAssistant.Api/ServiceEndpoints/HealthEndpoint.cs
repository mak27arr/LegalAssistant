using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Api.ServiceEndpoints;

public static class HealthEndpoint
{
    public static WebApplication MapHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/health", async (LegalAssistantDbContext db, IConfiguration config, CancellationToken ct) =>
        {
            if (!await db.Database.CanConnectAsync(ct))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            var embeddingsBase = config["Embeddings:BaseUrl"] ?? Environment.GetEnvironmentVariable("EMBEDDINGS_BASE_URL") ?? "http://embeddings";
            using var http = new HttpClient { BaseAddress = new Uri(embeddingsBase), Timeout = TimeSpan.FromSeconds(2) };

            try
            {
                using var response = await http.GetAsync("/health", ct);
                if (!response.IsSuccessStatusCode)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new { status = "ok" });
        });

        return app;
    }
}
