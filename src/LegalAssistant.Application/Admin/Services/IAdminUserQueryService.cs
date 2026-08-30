using LegalAssistant.Application.Admin.Models;

namespace LegalAssistant.Application.Admin.Services;

public interface IAdminUserQueryService
{
    Task<IReadOnlyList<AdminUserListItemResult>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminRoleResult>> GetRolesAsync(CancellationToken cancellationToken = default);
}
