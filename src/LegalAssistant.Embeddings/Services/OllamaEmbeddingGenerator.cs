using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Embeddings.Services;

public sealed class OllamaEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingGenerator> _logger;
    private readonly SemaphoreSlim _concurrency;
    private int? _dimensions;

    public OllamaEmbeddingGenerator(HttpClient httpClient, string model, ILogger<OllamaEmbeddingGenerator> logger)
    {
        _httpClient = httpClient;
        _model = model;
        _logger = logger;
        _concurrency = new SemaphoreSlim(5, 5);
    }

    public int Dimensions => _dimensions ?? 0;

    // Kept for interface compatibility; real implementation is async via GenerateAsync.
    public float[] Generate(string text) => throw new NotSupportedException("Use GenerateAsync for Ollama embeddings.");

    public async Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            const int maxRetries = 3;
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var payload = new { model = _model, prompt = text };
                    using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    using var resp = await _httpClient.PostAsync("/api/embeddings", content, cancellationToken);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning(
                            "Ollama embeddings non-success status {StatusCode} on attempt {Attempt}/{MaxAttempts}. BodyStart={BodyStart}",
                            (int)resp.StatusCode,
                            attempt,
                            maxRetries,
                            body.Length <= 256 ? body : body.Substring(0, 256));
                        resp.EnsureSuccessStatusCode();
                    }

                    var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
                    OllamaEmbeddingResponse? result = null;
                    try
                    {
                        result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(raw);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Ollama embeddings response could not be deserialized on attempt {Attempt}/{MaxAttempts}. BodyStart={BodyStart}",
                            attempt,
                            maxRetries,
                            raw.Length <= 256 ? raw : raw.Substring(0, 256));
                    }

                    var embedding = result?.Embedding ?? Array.Empty<float>();

                    if (embedding.Length == 0)
                    {
                        _logger.LogWarning(
                            "Ollama embeddings returned empty vector on attempt {Attempt}/{MaxAttempts}. BodyStart={BodyStart}",
                            attempt,
                            maxRetries,
                            raw.Length <= 256 ? raw : raw.Substring(0, 256));
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                            continue;
                        }

                        return Array.Empty<float>();
                    }

                    if (_dimensions.HasValue && embedding.Length != _dimensions.Value)
                    {
                        _logger.LogWarning(
                            "Ollama embeddings returned vector with unexpected dimensions. Expected={Expected} Actual={Actual}",
                            _dimensions.Value,
                            embedding.Length);
                    }

                    _dimensions ??= embedding.Length;
                    return embedding;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Ollama embeddings call failed on attempt {Attempt}/{MaxAttempts}", attempt, maxRetries);
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
                }
            }

            return Array.Empty<float>();
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private sealed record OllamaEmbeddingResponse([property: JsonPropertyName("embedding")] float[] Embedding);
}
