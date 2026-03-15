using System;

namespace LegalAssistant.Domain.Models
{
    public enum JobStatus { Queued, InProgress, Completed, Failed }

    public class JobRecord
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public JobStatus Status { get; set; }
        public string Payload { get; set; }
        public string? Result { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
