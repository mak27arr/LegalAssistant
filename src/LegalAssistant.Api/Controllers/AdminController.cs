using LegalAssistant.Api.Dtos.Admin;
using LegalAssistant.Api.Services.Auth;
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

    public AdminController(
        IAdminUserQueryService queries,
        IAdminUserRoleService roles,
        IAdminUserManagementService management)
    {
        _queries = queries;
        _roles = roles;
        _management = management;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _queries.GetUsersAsync(cancellationToken);
        return Ok(users.Select(MapUser).ToList());
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
