using LegalAssistant.Application.Admin.Models;
using LegalAssistant.Application.Admin.Services;
using LegalAssistant.Application.Admin;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Admin;

public sealed class AdminUserRoleService : IAdminUserRoleService
{
    private readonly LegalAssistantDbContext _db;

    public AdminUserRoleService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<AdminUserListItemResult?> UpdateRolesAsync(UpdateAdminUserRolesCommand command, CancellationToken cancellationToken = default)
    {
        var roleNames = command.RoleNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Append(RoleNames.User)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == command.UserId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        var roles = await _db.Roles
            .Where(x => roleNames.Contains(x.Name))
            .ToListAsync(cancellationToken);

        if (roles.Count != roleNames.Length)
        {
            var knownRoles = roles.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingRoles = roleNames.Where(x => !knownRoles.Contains(x)).ToArray();
            throw new ArgumentException($"Unknown roles: {string.Join(", ", missingRoles)}");
        }

        var desiredRoleIds = roles.Select(x => x.Id).ToHashSet();
        var currentRoleIds = user.UserRoles.Select(x => x.RoleId).ToHashSet();

        var toRemove = user.UserRoles.Where(x => !desiredRoleIds.Contains(x.RoleId)).ToList();
        foreach (var userRole in toRemove)
        {
            user.UserRoles.Remove(userRole);
        }
        _db.UserRoles.RemoveRange(toRemove);

        foreach (var role in roles.Where(x => !currentRoleIds.Contains(x.Id)))
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                User = user,
                Role = role
            });
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var resultRoles = user.UserRoles
            .Select(x => x.Role.Name)
            .OrderBy(x => x)
            .ToArray();

        return new AdminUserListItemResult(
            user.Id,
            user.Email,
            user.FullName,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            resultRoles);
    }
}
