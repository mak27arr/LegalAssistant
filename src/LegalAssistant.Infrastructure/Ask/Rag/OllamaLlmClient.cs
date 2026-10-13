using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LegalAssistant.Application.Rag;
using Microsoft.Extensions.Logging;

namespace LegalAssistant.Infrastructure.Rag;

public sealed class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaLlmClient> _logger;

    public OllamaLlmClient(HttpClient http, ILogger<OllamaLlmClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return string.Empty;

        var model = Environment.GetEnvironmentVariable("OLLAMA_LLM_MODEL")
                  ?? Environment.GetEnvironmentVariable("Ollama__LlmModel")
                  ?? "mistral";

        var request = new OllamaGenerateRequest(model, prompt, Stream: false);
        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("api/generate", content, cancellationToken);
        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Ollama generate failed. Url={Url} Status={Status} BodyStart={BodyStart}",
                _http.BaseAddress,
                (int)resp.StatusCode,
                raw.Length <= 256 ? raw : raw.Substring(0, 256));
            resp.EnsureSuccessStatusCode();
        }

        var dto = JsonSerializer.Deserialize<OllamaGenerateResponse>(raw);
        return dto?.Response ?? string.Empty;
    }

    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record OllamaGenerateResponse([property: JsonPropertyName("response")] string Response);
 }
