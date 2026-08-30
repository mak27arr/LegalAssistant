using LegalAssistant.Application.Admin.Models;
using LegalAssistant.Application.Admin.Services;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Admin;

public sealed class AdminUserQueryService : IAdminUserQueryService
{
    private readonly LegalAssistantDbContext _db;

    public AdminUserQueryService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminUserListItemResult>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => new AdminUserListItemResult(
                x.Id,
                x.Email,
                x.FullName,
                x.IsActive,
                x.CreatedAt,
                x.LastLoginAt,
                x.UserRoles
                    .OrderBy(ur => ur.Role.Name)
                    .Select(ur => ur.Role.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminRoleResult>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminRoleResult(x.Id, x.Name, x.Description))
            .ToListAsync(cancellationToken);
    }
}
