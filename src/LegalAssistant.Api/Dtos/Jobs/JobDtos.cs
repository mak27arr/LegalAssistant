namespace LegalAssistant.Api.Dtos.Jobs;

public sealed record JobDto(
    Guid Id,
    string Type,
    string Status,
    string Payload,
    string? Result,
    DateTime CreatedAt,
    DateTime UpdatedAt);
