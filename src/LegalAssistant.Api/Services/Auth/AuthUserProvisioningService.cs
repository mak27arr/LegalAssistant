using LegalAssistant.Api.Configuration;
using LegalAssistant.Api.Services.Auth.Constants;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Api.Services.Auth;

public sealed class AuthUserProvisioningService : IAuthUserProvisioningService
{
    private readonly LegalAssistantDbContext _db;
    private readonly IOptions<AuthOptions> _authOptions;

    public AuthUserProvisioningService(LegalAssistantDbContext db, IOptions<AuthOptions> authOptions)
    {
        _db = db;
        _authOptions = authOptions;
    }

    public async Task<AuthenticatedUser> ProvisionGoogleUserAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken)
    {
        var normalizedEmail = googleUser.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.GoogleSubjectId == googleUser.Subject || x.Email == normalizedEmail,
                cancellationToken);

        var defaultRole = await _db.Roles.SingleAsync(x => x.Name == RoleNames.User, cancellationToken);
        var adminRole = await _db.Roles.SingleAsync(x => x.Name == RoleNames.Admin, cancellationToken);
        var adminEmails = _authOptions.Value.Bootstrap.AdminEmails
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = googleUser.FullName,
                GoogleSubjectId = googleUser.Subject,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id, User = user, Role = defaultRole });

            if (adminEmails.Contains(normalizedEmail))
            {
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id, User = user, Role = adminRole });
            }

            _db.Users.Add(user);
        }
        else
        {
            user.Email = normalizedEmail;
            user.FullName = googleUser.FullName;
            user.GoogleSubjectId = googleUser.Subject;
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            user.IsActive = true;

            if (!user.UserRoles.Any(x => x.RoleId == defaultRole.Id))
            {
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id, User = user, Role = defaultRole });
            }

            if (adminEmails.Contains(normalizedEmail) && !user.UserRoles.Any(x => x.RoleId == adminRole.Id))
            {
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id, User = user, Role = adminRole });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(x => x.Role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new AuthenticatedUser(user.Id, user.Email, user.FullName, roles);
    }
}
