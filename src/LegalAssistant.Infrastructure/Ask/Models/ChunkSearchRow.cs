using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegalAssistant.Infrastructure.Ask.Models
{
    internal sealed class ChunkSearchProjection
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public int ChunkIndex { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        public double Score { get; set; }
    }
}
