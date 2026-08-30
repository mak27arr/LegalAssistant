using System.Linq.Expressions;
using LegalAssistant.Application.Admin.Models;
using LegalAssistant.Application.Admin.Services;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Admin;

public sealed class AdminUserManagementService : IAdminUserManagementService
{
    private readonly LegalAssistantDbContext _db;

    public AdminUserManagementService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<AdminUserDetailsResult?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(MapDetails())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminUserDetailsResult?> SetBlockedAsync(Guid userId, bool isBlocked, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.RefreshTokens)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        user.IsActive = !isBlocked;
        user.UpdatedAt = DateTime.UtcNow;

        if (isBlocked)
        {
            foreach (var refreshToken in user.RefreshTokens.Where(x => x.RevokedAt == null))
            {
                refreshToken.RevokedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new AdminUserDetailsResult(
            user.Id,
            user.Email,
            user.FullName,
            user.GoogleSubjectId,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray());
    }

    private static Expression<Func<User, AdminUserDetailsResult>> MapDetails()
    {
        return x => new AdminUserDetailsResult(
            x.Id,
            x.Email,
            x.FullName,
            x.GoogleSubjectId,
            x.IsActive,
            x.CreatedAt,
            x.UpdatedAt,
            x.LastLoginAt,
            x.UserRoles.OrderBy(ur => ur.Role.Name).Select(ur => ur.Role.Name).ToList());
    }
}
