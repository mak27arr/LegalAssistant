using System.Text;
using System.Text.Json;
using LegalAssistant.Application.Embeddings;
using LegalAssistant.Core.Correlation;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Embeddings;

public sealed class HttpEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpEmbeddingClient> _logger;
    private readonly ICorrelationContext _correlation;

    public HttpEmbeddingClient(HttpClient httpClient, ILogger<HttpEmbeddingClient> logger, ICorrelationContext correlation)
    {
        _httpClient = httpClient;
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        var correlationId = string.IsNullOrWhiteSpace(_correlation.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : _correlation.CorrelationId;

        _correlation.CorrelationId = correlationId;
        using var scope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
        {
            ["correlationId"] = correlationId
        });

        _logger.LogInformation("Embedding request started");

        using var body = new StringContent(JsonSerializer.Serialize(new { text }), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/embed")
        {
            Content = body
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        _logger.LogInformation("Embedding response received with status {StatusCode}", (int)response.StatusCode);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var vector = await JsonSerializer.DeserializeAsync<float[]>(stream, cancellationToken: cancellationToken);

        _logger.LogInformation("Embedding request completed. Dimensions={Dimensions}", vector?.Length ?? 0);
        return vector ?? Array.Empty<float>();
    }
}
