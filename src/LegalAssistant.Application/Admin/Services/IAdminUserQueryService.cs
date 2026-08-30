using LegalAssistant.Application.Admin.Models;

namespace LegalAssistant.Application.Admin.Services;

public interface IAdminUserQueryService
{
    Task<AdminUserListPageResult> GetUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminRoleResult>> GetRolesAsync(CancellationToken cancellationToken = default);
}
