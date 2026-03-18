using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LegalAssistant.Embeddings.Services;

public sealed class OllamaEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private int? _dimensions;

    public OllamaEmbeddingGenerator(HttpClient httpClient, string model)
    {
        _httpClient = httpClient;
        _model = model;
    }

    public int Dimensions => _dimensions ?? 0;

    // Kept for interface compatibility; real implementation is async via GenerateAsync.
    public float[] Generate(string text) => throw new NotSupportedException("Use GenerateAsync for Ollama embeddings.");

    public async Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        var payload = new { model = _model, prompt = text };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await _httpClient.PostAsync("/api/embeddings", content, cancellationToken);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<OllamaEmbeddingResponse>(stream, cancellationToken: cancellationToken);

        var embedding = result?.Embedding ?? Array.Empty<float>();
        _dimensions ??= embedding.Length;
        return embedding;
    }

    private sealed record OllamaEmbeddingResponse(float[] Embedding);
}
