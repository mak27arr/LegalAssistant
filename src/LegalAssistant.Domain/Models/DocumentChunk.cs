using System;
using Pgvector;

namespace LegalAssistant.Domain.Models
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Guid? ChunkingRunId { get; set; }
        public int ChunkIndex { get; set; }
        public string Text { get; set; }
        public string CharRange { get; set; }
        public string SourceUrl { get; set; }
        public Vector? Embedding { get; set; }
        public DateTime CreatedAt { get; set; }

        public Document Document { get; set; }
    }
}
