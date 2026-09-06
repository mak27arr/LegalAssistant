using System.Text.Json;
using System.Text.Json.Serialization;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Jobs.Models;

public sealed record IngestJobResult(int Chunks, EmbeddingStatus Embeddings);

public static class IngestJobResultSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(int chunks, EmbeddingStatus embeddings)
        => JsonSerializer.Serialize(new IngestJobResult(chunks, embeddings), JsonOptions);
}
