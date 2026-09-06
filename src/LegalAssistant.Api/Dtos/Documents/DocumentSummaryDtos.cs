namespace LegalAssistant.Api.Dtos.Documents;

public sealed record DocumentListItemDto(
    Guid Id,
    string Title,
    string Url,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ChunkCount,
    string? ProcessingStatus);

public sealed record DocumentListPageDto(
    IReadOnlyList<DocumentListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record DocumentDetailsDto(
    Guid Id,
    string Title,
    string Url,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ChunkCount,
    string? ProcessingStatus,
    int EmbeddingCount,
    int CompletedEmbeddingCount,
    int FailedEmbeddingCount);
