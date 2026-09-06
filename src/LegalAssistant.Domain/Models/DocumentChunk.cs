using System;

namespace LegalAssistant.Domain.Models
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid? ChunkingRunId { get; set; }
        public int ChunkIndex { get; set; }
        public required string Text { get; set; }
        public required string CharRange { get; set; }
        public required string SourceUrl { get; set; }
        public EmbeddingVector? Embedding { get; set; }
        public EmbeddingStatus EmbeddingStatus { get; set; } = EmbeddingStatus.Pending;
        public int EmbeddingAttemptCount { get; set; }
        public string? EmbeddingLastError { get; set; }
        public DateTime? EmbeddingStartedAt { get; set; }
        public DateTime? EmbeddingCompletedAt { get; set; }
        public DateTime? EmbeddingFailedAt { get; set; }
        public DateTime? EmbeddingUpdatedAt { get; set; }
        public Guid? JobId { get; set; }
        public DateTime CreatedAt { get; set; }

        public Document? Document { get; set; }
    }
}
