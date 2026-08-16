using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace LegalAssistant.Infrastructure.Health;

public sealed class PostgresConnectionHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public PostgresConnectionHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = ResolveValue(
            _configuration.GetConnectionString("DefaultConnection"),
            _configuration["ConnectionStrings:DefaultConnection"],
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString))
            return HealthCheckResult.Unhealthy("Missing PostgreSQL connection string.");

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connection failed.", ex);
        }
    }

    private static string? ResolveValue(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

public sealed class ApiEmbeddingsReadinessHealthCheck : ConfiguredHttpHealthCheck
{
    protected override string BaseUrl => ResolveValue(
        Configuration["Embeddings:BaseUrl"],
        Environment.GetEnvironmentVariable("Embeddings__BaseUrl"),
        "http://embeddings") ?? "http://embeddings";

    protected override string Path => "/health/ready";

    public ApiEmbeddingsReadinessHealthCheck(IConfiguration configuration) : base(configuration) { }
}

public sealed class ApiOllamaReadinessHealthCheck : ConfiguredHttpHealthCheck
{
    protected override string BaseUrl => ResolveValue(
        Configuration["Ollama:BaseUrl"],
        Environment.GetEnvironmentVariable("Ollama__BaseUrl"),
        Environment.GetEnvironmentVariable("OLLAMA_BASE_URL"),
        "http://ollama:11434") ?? "http://ollama:11434";

    protected override string Path => "/api/version";

    public ApiOllamaReadinessHealthCheck(IConfiguration configuration) : base(configuration) { }
}

public sealed class EmbeddingsOllamaReadinessHealthCheck : ConfiguredHttpHealthCheck
{
    protected override string BaseUrl => ResolveValue(
        Configuration["Ollama:BaseUrl"],
        Environment.GetEnvironmentVariable("Ollama__BaseUrl"),
        Environment.GetEnvironmentVariable("OLLAMA_BASE_URL"),
        "http://ollama:11434") ?? "http://ollama:11434";

    protected override string Path => "/api/version";

    public EmbeddingsOllamaReadinessHealthCheck(IConfiguration configuration) : base(configuration) { }
}

public sealed class RabbitMqTcpHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public RabbitMqTcpHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var host = ResolveValue(
            _configuration["RabbitMq:Host"],
            Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
            "rabbitmq") ?? "rabbitmq";

        var portValue = ResolveValue(
            _configuration["RabbitMq:Port"],
            Environment.GetEnvironmentVariable("RABBITMQ_PORT"),
            "5672") ?? "5672";

        if (!int.TryParse(portValue, out var port))
            return HealthCheckResult.Unhealthy("RabbitMQ port is invalid.");

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout, cancellationToken));

            if (completed != connectTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return HealthCheckResult.Unhealthy($"RabbitMQ connection timed out to {host}:{port}.");
            }

            await connectTask;
            return HealthCheckResult.Healthy("RabbitMQ is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection failed.", ex);
        }
    }

    private static string? ResolveValue(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

public abstract class ConfiguredHttpHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    protected ConfiguredHttpHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected IConfiguration Configuration => _configuration;

    protected abstract string BaseUrl { get; }
    protected abstract string Path { get; }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var baseUrl = BaseUrl ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl))
            return HealthCheckResult.Unhealthy("Health check base URL is missing.");

        var uri = new Uri(new Uri(EnsureTrailingSlash(baseUrl)), Path.TrimStart('/'));

        try
        {
            using var http = new HttpClient { Timeout = Timeout };
            using var response = await http.GetAsync(uri, cancellationToken);

            if (response.IsSuccessStatusCode)
                return HealthCheckResult.Healthy($"{uri} is reachable.");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = body.Length <= 256 ? body : body[..256];
            return HealthCheckResult.Unhealthy($"{uri} returned {(int)response.StatusCode}. {detail}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{uri} check failed.", ex);
        }
    }

    protected static string? ResolveValue(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith('/') ? value : value + "/";
}

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddApiReadinessHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresConnectionHealthCheck>("postgres", tags: new[] { "ready" })
            .AddCheck<ApiEmbeddingsReadinessHealthCheck>("embeddings", tags: new[] { "ready" })
            .AddCheck<ApiOllamaReadinessHealthCheck>("ollama", tags: new[] { "ready" });

        return services;
    }

    public static IServiceCollection AddEmbeddingsReadinessHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<RabbitMqTcpHealthCheck>("rabbitmq", tags: new[] { "ready" });

        var embeddingMode = (configuration["Embeddings:Mode"] ?? "ollama").Trim().ToLowerInvariant();
        if (embeddingMode != "mock")
        {
            services.AddHealthChecks()
                .AddCheck<EmbeddingsOllamaReadinessHealthCheck>("ollama", tags: new[] { "ready" });
        }

        return services;
    }
}
