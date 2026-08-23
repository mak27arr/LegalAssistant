namespace LegalAssistant.Api.Dtos.Chunks;

public record PageResponse<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record ChunkListItemDto(
    Guid ChunkId,
    Guid DocumentId,
    int ChunkIndex,
    string CharRange,
    string SourceUrl,
    DateTime CreatedAt,
    bool HasEmbedding,
    string Preview);

public sealed record ChunkPageResponse(
    IReadOnlyList<ChunkListItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
    : PageResponse<ChunkListItemDto>(
        Items,
        Page,
        PageSize,
        TotalItems,
        TotalPages,
        HasNextPage,
        HasPreviousPage);

public sealed record ChunkDetailsDto(
    Guid ChunkId,
    Guid DocumentId,
    int ChunkIndex,
    string Text,
    string CharRange,
    string SourceUrl,
    DateTime CreatedAt,
    bool HasEmbedding);
