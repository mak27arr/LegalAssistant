namespace LegalAssistant.Application.Admin.Models;

public sealed record AdminUserListPageResult(
    IReadOnlyList<AdminUserListItemResult> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
