namespace LegalAssistant.Application.Admin.Models;

public sealed record UpdateAdminUserRolesCommand(
    Guid UserId,
    IReadOnlyList<string> RoleNames);
