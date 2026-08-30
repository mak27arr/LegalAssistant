namespace LegalAssistant.Api.Dtos.Admin;

public sealed record AdminRoleDto(
    string Id,
    string Name,
    string? Description);

public sealed record AdminUserDto(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);

public sealed record AdminUserDetailsDto(
    string Id,
    string Email,
    string FullName,
    string GoogleSubjectId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles);

public sealed record UpdateAdminUserRolesRequest(
    IReadOnlyList<string> Roles);
