using LegalAssistant.Infrastructure.Health;
using Microsoft.Extensions.Configuration;

namespace LegalAssistant.Api.Health;

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
