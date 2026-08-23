namespace LegalAssistant.Application.Chunks.Models;

public sealed record DocumentChunkPageResult(
    IReadOnlyList<DocumentChunkListItemResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
