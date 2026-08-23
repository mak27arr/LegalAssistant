namespace LegalAssistant.Application.Documents.Models;

public sealed record DocumentDetailsResult(
    Guid Id,
    string Title,
    string Url,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ChunkCount);
