using LegalAssistant.Api.Dtos.Admin;
using LegalAssistant.Api.Services.Auth;
using LegalAssistant.Application.Auth;
using LegalAssistant.Application.Admin;
using LegalAssistant.Application.Admin.Models;
using LegalAssistant.Application.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminUserQueryService _queries;
    private readonly IAdminUserRoleService _roles;
    private readonly IAdminUserManagementService _management;
    private readonly IUserSessionManager _sessions;

    public AdminController(
        IAdminUserQueryService queries,
        IAdminUserRoleService roles,
        IAdminUserManagementService management,
        IUserSessionManager sessions)
    {
        _queries = queries;
        _roles = roles;
        _management = management;
        _sessions = sessions;
    }

    [HttpGet("users")]
    public async Task<ActionResult<AdminUserPageDto>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _queries.GetUsersAsync(new AdminUserListQuery(search, status, sort, page, pageSize), cancellationToken);
        return Ok(new AdminUserPageDto(
            result.Items.Select(MapUser).ToList(),
            result.Page,
            result.PageSize,
            result.TotalItems,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<AdminRoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _queries.GetRolesAsync(cancellationToken);
        return Ok(roles.Select(MapRole).ToList());
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailsDto>> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _management.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(MapUserDetails(user));
    }

    [HttpPut("users/{userId:guid}/roles")]
    public async Task<ActionResult<AdminUserDto>> UpdateUserRoles(
        Guid userId,
        [FromBody] UpdateAdminUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var requestedRoles = request.Roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var currentUser = User.ToAuthenticatedUser();
        if (currentUser.Id == userId && !requestedRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("You cannot remove the Admin role from your own account.");
        }

        var updated = await _roles.UpdateRolesAsync(new UpdateAdminUserRolesCommand(userId, requestedRoles), cancellationToken);
        if (updated == null)
        {
            return NotFound();
        }

        await _sessions.RevokeUserSessionsAsync(userId, cancellationToken);
        return Ok(MapUser(updated));
    }

    [HttpPost("users/{userId:guid}/block")]
    public async Task<ActionResult<AdminUserDetailsDto>> BlockUser(Guid userId, CancellationToken cancellationToken)
    {
        var currentUser = User.ToAuthenticatedUser();
        if (currentUser.Id == userId)
        {
            return BadRequest("You cannot block your own account.");
        }

        var updated = await _management.SetBlockedAsync(userId, isBlocked: true, cancellationToken);
        if (updated == null)
        {
            return NotFound();
        }

        await _sessions.RevokeUserSessionsAsync(userId, cancellationToken);
        return Ok(MapUserDetails(updated));
    }

    [HttpPost("users/{userId:guid}/unblock")]
    public async Task<ActionResult<AdminUserDetailsDto>> UnblockUser(Guid userId, CancellationToken cancellationToken)
    {
        var updated = await _management.SetBlockedAsync(userId, isBlocked: false, cancellationToken);
        if (updated == null)
        {
            return NotFound();
        }

        return Ok(MapUserDetails(updated));
    }

    private static AdminUserDto MapUser(AdminUserListItemResult user)
        => new(
            user.Id.ToString(),
            user.Email,
            user.FullName,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            user.Roles);

    private static AdminRoleDto MapRole(AdminRoleResult role)
        => new(
            role.Id.ToString(),
            role.Name,
            role.Description);

    private static AdminUserDetailsDto MapUserDetails(AdminUserDetailsResult user)
        => new(
            user.Id.ToString(),
            user.Email,
            user.FullName,
            user.GoogleSubjectId,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt,
            user.Roles);
}
