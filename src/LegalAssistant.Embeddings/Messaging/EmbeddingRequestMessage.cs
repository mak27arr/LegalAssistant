using System.Text.Json.Serialization;

namespace LegalAssistant.Embeddings.Messaging;

public sealed record EmbeddingRequestMessage
{
    [JsonPropertyName("chunkId")]
    public Guid ChunkId { get; init; }

    [JsonPropertyName("chunk_id")]
    public Guid Chunk_Id { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonIgnore]
    public Guid EffectiveChunkId => ChunkId == Guid.Empty ? Chunk_Id : ChunkId;
}
