namespace LegalAssistant.Application.Admin.Models;

public sealed record AdminUserListQuery(
    string? Search,
    string? Status,
    string? Sort,
    int Page,
    int PageSize);
