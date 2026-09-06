using System;

namespace LegalAssistant.Domain.Models
{
    public sealed class ChunkingRun
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }

        public required string StrategyName { get; set; }
        public required string StrategyVersion { get; set; }
        public required string ParamsJson { get; set; }

        public ChunkingRunStatus Status { get; set; } = ChunkingRunStatus.InProgress;
        public int TotalChunks { get; set; }
        public int CompletedChunks { get; set; }
        public int FailedChunks { get; set; }
        public string? LastError { get; set; }
        public DateTime? EmbeddingCompletedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Document? Document { get; set; }
    }
}
