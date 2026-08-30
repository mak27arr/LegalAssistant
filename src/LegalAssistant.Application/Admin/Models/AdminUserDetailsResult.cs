namespace LegalAssistant.Application.Admin.Models;

public sealed record AdminUserDetailsResult(
    Guid Id,
    string Email,
    string FullName,
    string GoogleSubjectId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);
