using System;
using System.Collections.Generic;

namespace LegalAssistant.Domain.Models
{
    public class Document
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public string Metadata { get; set; }
        public int Version { get; set; }
        public bool IsDeleted { get; set; }
        public Guid? ActiveChunkingRunId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<DocumentChunk> Chunks { get; set; }
        public ICollection<ChunkingRun> ChunkingRuns { get; set; }
    }
}
