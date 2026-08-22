using System;

namespace LegalAssistant.Domain.Models;

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Published,
    Failed
}

public class OutboxMessageRecord
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public required string MessageType { get; set; }
    public required string RoutingKey { get; set; }
    public required string Payload { get; set; }
    public required string CorrelationId { get; set; }
    public OutboxMessageStatus Status { get; set; }
    public int Attempts { get; set; }
    public int Version { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
