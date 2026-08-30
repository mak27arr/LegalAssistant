using LegalAssistant.Application.Admin.Models;
using LegalAssistant.Application.Admin.Services;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace LegalAssistant.Infrastructure.Admin;

public sealed class AdminUserQueryService : IAdminUserQueryService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private readonly LegalAssistantDbContext _db;

    public AdminUserQueryService(LegalAssistantDbContext db)
    {
        _db = db;
    }

    public async Task<AdminUserListPageResult> GetUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page <= 0 ? DefaultPage : query.Page;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        var search = query.Search?.Trim();
        var status = query.Status?.Trim().ToLowerInvariant();
        var sort = query.Sort?.Trim().ToLowerInvariant();

        IQueryable<Domain.Models.User> users = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.ToLowerInvariant();
            users = users.Where(x =>
                x.Email.ToLower().Contains(searchTerm) ||
                x.FullName.ToLower().Contains(searchTerm));
        }

        users = status switch
        {
            "active" => users.Where(x => x.IsActive),
            "blocked" => users.Where(x => !x.IsActive),
            _ => users
        };

        users = sort switch
        {
            "name_asc" => users.OrderBy(x => x.FullName).ThenBy(x => x.Email),
            "name_desc" => users.OrderByDescending(x => x.FullName).ThenBy(x => x.Email),
            "email_asc" => users.OrderBy(x => x.Email),
            "email_desc" => users.OrderByDescending(x => x.Email),
            "created_asc" => users.OrderBy(x => x.CreatedAt).ThenBy(x => x.Email),
            "created_desc" => users.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Email),
            "last_login_asc" => users.OrderBy(x => x.LastLoginAt == null).ThenBy(x => x.LastLoginAt).ThenBy(x => x.Email),
            _ => users.OrderBy(x => x.LastLoginAt == null).ThenByDescending(x => x.LastLoginAt).ThenBy(x => x.Email)
        };

        var totalItems = await users.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Min(page, totalPages);

        var items = await users
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return new AdminUserListPageResult(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            page < totalPages,
            page > 1);
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
