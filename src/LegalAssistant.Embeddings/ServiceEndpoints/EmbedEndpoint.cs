using LegalAssistant.Embeddings.Contracts;
using LegalAssistant.Embeddings.Services;

namespace LegalAssistant.Embeddings.ServiceEndpoints;

public static class EmbedEndpoint
{
    public static WebApplication MapEmbedEndpoint(this WebApplication app)
    {
        app.MapPost("/embed", async (HttpContext http, EmbedRequest req, IEmbeddingGenerator generator, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var correlationId = http.Request.Headers["X-Correlation-Id"].ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
                correlationId = Guid.NewGuid().ToString("N");

            http.Response.Headers["X-Correlation-Id"] = correlationId;
            var logger = loggerFactory.CreateLogger("Embed");
            using var _ = logger.BeginScope(new Dictionary<string, object> { ["correlationId"] = correlationId });

            if (string.IsNullOrWhiteSpace(req.Text))
                return Results.BadRequest("Text is required");

            logger.LogInformation("Embedding request received");
            var vector = await generator.GenerateAsync(req.Text, ct);
            logger.LogInformation("Embedding response generated. Dimensions={Dimensions}", vector.Length);
            return Results.Ok(vector);
        });

        return app;
    }
}
