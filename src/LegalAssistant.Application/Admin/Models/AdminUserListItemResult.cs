namespace LegalAssistant.Application.Admin.Models;

public sealed record AdminUserListItemResult(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);
