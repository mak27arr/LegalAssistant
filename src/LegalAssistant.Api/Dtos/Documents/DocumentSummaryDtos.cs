namespace LegalAssistant.Api.Dtos.Documents;

public sealed record DocumentListItemDto(
    Guid Id,
    string Title,
    string Url,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ChunkCount);

public sealed record DocumentDetailsDto(
    Guid Id,
    string Title,
    string Url,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ChunkCount);
