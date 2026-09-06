namespace LegalAssistant.Embeddings.Messaging;

public sealed record EmbeddingCompletedMessage(Guid ChunkId, float[] Vector);
