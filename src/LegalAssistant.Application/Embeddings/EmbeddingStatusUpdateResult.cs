using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Embeddings;

public sealed record EmbeddingStatusUpdateResult(
    bool ChunkFound,
    bool RunCompleted,
    bool RunFailed,
    int TotalChunks,
    int CompletedChunks,
    int FailedChunks,
    JobStatus? JobStatus);
