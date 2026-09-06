using System.Text.Json;
using LegalAssistant.Application.Messaging;
using LegalAssistant.Domain.Models;

namespace LegalAssistant.Application.Ask.Models;

public static class AskJobOutboxFactory
{
    public static OutboxMessageRecord Create(AskJobEventRecord eventRecord, DateTime occurredAtUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            JobId = eventRecord.JobId,
            AskJobEvent = eventRecord,
            MessageType = AskJobMessageNames.GetMessageType(eventRecord.Status),
            RoutingKey = AskJobMessageNames.GetRoutingKey(eventRecord.Status),
            Payload = JsonSerializer.Serialize(eventRecord),
            CorrelationId = eventRecord.JobId.ToString("N"),
            Status = OutboxMessageStatus.Pending,
            Attempts = 0,
            Version = 1,
            CreatedAt = occurredAtUtc,
            UpdatedAt = occurredAtUtc
        };
}
