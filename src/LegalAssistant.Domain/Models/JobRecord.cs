using System;

namespace LegalAssistant.Domain.Models
{
    public enum JobStatus { Queued, InProgress, EmbeddingInProgress, Completed, Failed }

    public class JobRecord
    {
        public Guid Id { get; set; }
        public required string Type { get; set; }
        public JobStatus Status { get; set; }
        public required string Payload { get; set; }
        public string? Result { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime? StartedAt { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime? LeaseExpiresAt { get; set; }
        public Guid? LeaseId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
