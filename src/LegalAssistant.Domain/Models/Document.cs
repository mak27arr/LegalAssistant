using System;
using System.Collections.Generic;

namespace LegalAssistant.Domain.Models
{
    public class Document
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Url { get; set; }
        public required string Content { get; set; }
        public required string Metadata { get; set; }
        public int Version { get; set; }
        public bool IsDeleted { get; set; }
        public Guid? ActiveChunkingRunId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
        public ICollection<ChunkingRun> ChunkingRuns { get; set; } = new List<ChunkingRun>();
    }
}
