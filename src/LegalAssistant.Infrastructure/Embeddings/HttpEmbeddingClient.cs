using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Embeddings;

namespace LegalAssistant.Infrastructure.Embeddings;

public sealed class HttpEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;

    public HttpEmbeddingClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        using var body = new StringContent(JsonSerializer.Serialize(new { text }), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/embed", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var vector = await JsonSerializer.DeserializeAsync<float[]>(stream, cancellationToken: cancellationToken);
        return vector ?? Array.Empty<float>();
    }
}
