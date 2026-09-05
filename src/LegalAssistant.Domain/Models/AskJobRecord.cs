using System;

namespace LegalAssistant.Domain.Models;

public enum AskJobStatus
{
    Queued,
    InProgress,
    Completed,
    Failed
}

public class AskJobRecord
{
    public Guid Id { get; set; }
    public Guid? OwnerUserId { get; set; }
    public required string ActorScopeKey { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string Question { get; set; }
    public int TopK { get; set; }
    public string? ConversationId { get; set; }
    public required string RequestHash { get; set; }
    public AskJobStatus Status { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? OwnerUser { get; set; }
}
