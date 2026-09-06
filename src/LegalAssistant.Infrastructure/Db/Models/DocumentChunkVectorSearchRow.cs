using LegalAssistant.Domain.Models;
using Pgvector;

namespace LegalAssistant.Infrastructure.Db.Models;

/// <summary>
/// Read-only persistence model used for pgvector queries.
/// The domain model intentionally keeps its provider-independent EmbeddingVector.
/// </summary>
public sealed class DocumentChunkVectorSearchRow
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public required string Text { get; set; }
    public required string CharRange { get; set; }
    public required string SourceUrl { get; set; }
    public Vector? Embedding { get; set; }
    public EmbeddingStatus EmbeddingStatus { get; set; }
    public int EmbeddingAttemptCount { get; set; }
    public string? EmbeddingLastError { get; set; }
    public DateTime? EmbeddingStartedAt { get; set; }
    public DateTime? EmbeddingCompletedAt { get; set; }
    public DateTime? EmbeddingFailedAt { get; set; }
    public DateTime? EmbeddingUpdatedAt { get; set; }
    public Guid? JobId { get; set; }
    public Guid? ChunkingRunId { get; set; }
    public DateTime CreatedAt { get; set; }
}
