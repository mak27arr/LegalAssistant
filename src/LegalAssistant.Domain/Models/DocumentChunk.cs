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
        public DateTime CreatedAt { get; set; }

        public Document? Document { get; set; }
    }
}
