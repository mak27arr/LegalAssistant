using LegalAssistant.Application.Admin.Models;

namespace LegalAssistant.Application.Admin.Services;

public interface IAdminUserManagementService
{
    Task<AdminUserDetailsResult?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AdminUserDetailsResult?> SetBlockedAsync(Guid userId, bool isBlocked, CancellationToken cancellationToken = default);
}
