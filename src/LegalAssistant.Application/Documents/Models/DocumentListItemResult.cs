namespace LegalAssistant.Application.Documents.Models;

public sealed record DocumentListItemResult(
    Guid Id,
    string Title,
    string Url,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ChunkCount,
    string? ProcessingStatus);
