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

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Document? Document { get; set; }
    }
}
