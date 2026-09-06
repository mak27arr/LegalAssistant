using RabbitMQ.Client;

namespace LegalAssistant.Messaging;

public sealed record RabbitMqMessageMetadata
{
    public string? MessageId { get; init; }
    public string? CorrelationId { get; init; }
    public string? MessageType { get; init; }
    public string ContentType { get; init; } = "application/json";
    public bool Persistent { get; init; } = true;
    public string? Expiration { get; init; }
    public IReadOnlyDictionary<string, object>? Headers { get; init; }

    public static RabbitMqMessageMetadata FromProperties(IBasicProperties? properties)
        => new()
        {
            MessageId = properties?.MessageId,
            CorrelationId = properties?.CorrelationId,
            MessageType = properties?.Type,
            ContentType = string.IsNullOrWhiteSpace(properties?.ContentType)
                ? "application/json"
                : properties!.ContentType,
            Persistent = properties?.Persistent ?? true,
            Expiration = properties?.Expiration,
            Headers = properties?.Headers is null
                ? null
                : new Dictionary<string, object>(properties.Headers)
        };

    public Dictionary<string, object> CopyHeaders()
        => Headers is null
            ? new(StringComparer.Ordinal)
            : new(Headers, StringComparer.Ordinal);
}
