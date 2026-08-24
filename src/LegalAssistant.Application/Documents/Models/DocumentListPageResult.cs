namespace LegalAssistant.Application.Documents.Models;

public sealed record DocumentListPageResult(
    IReadOnlyList<DocumentListItemResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
