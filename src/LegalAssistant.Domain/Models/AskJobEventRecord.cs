using System;

namespace LegalAssistant.Domain.Models;

public class AskJobEventRecord
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public required string ActorScopeKey { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string Question { get; set; }
    public int TopK { get; set; }
    public string? ConversationId { get; set; }
    public AskJobStatus Status { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}
