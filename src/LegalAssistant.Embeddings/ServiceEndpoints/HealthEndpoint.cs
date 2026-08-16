using LegalAssistant.Embeddings.Messaging;
using RabbitMQ.Client;

namespace LegalAssistant.Embeddings.ServiceEndpoints;

public static class HealthEndpoint
{
    public static WebApplication MapHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/health", async (RabbitMqOptions options, IConfiguration config, CancellationToken ct) =>
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = options.Host,
                    Port = options.Port,
                    UserName = options.User,
                    Password = options.Pass,
                    AutomaticRecoveryEnabled = false
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var ollamaBase = config["Ollama:BaseUrl"] ?? Environment.GetEnvironmentVariable("OLLAMA_BASEURL") ?? "http://ollama:11434";
            using var http = new HttpClient { BaseAddress = new Uri(ollamaBase), Timeout = TimeSpan.FromSeconds(2) };

            try
            {
                using var response = await http.GetAsync("/api/version", ct);
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
