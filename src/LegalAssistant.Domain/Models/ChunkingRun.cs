using System;

namespace LegalAssistant.Domain.Models
{
    public sealed class ChunkingRun
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }

        public string StrategyName { get; set; }
        public string StrategyVersion { get; set; }
        public string ParamsJson { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Document Document { get; set; }
    }
}
