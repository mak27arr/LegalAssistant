using LegalAssistant.Application.Admin.Models;

namespace LegalAssistant.Application.Admin.Services;

public interface IAdminUserRoleService
{
    Task<AdminUserListItemResult?> UpdateRolesAsync(UpdateAdminUserRolesCommand command, CancellationToken cancellationToken = default);
}
