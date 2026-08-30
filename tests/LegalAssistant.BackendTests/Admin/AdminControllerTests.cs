using System.Security.Claims;
using LegalAssistant.Api.Controllers;
using LegalAssistant.Api.Dtos.Admin;
using LegalAssistant.Application.Admin;
using LegalAssistant.Application.Admin.Models;
using LegalAssistant.Application.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LegalAssistant.BackendTests.Admin;

public sealed class AdminControllerTests
{
    [Fact]
    public async Task GetUsers_ReturnsMappedUsers()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        queryService
            .Setup(x => x.GetUsersAsync(It.IsAny<AdminUserListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUserListPageResult(
                [
                    new AdminUserListItemResult(
                        Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        "user@example.com",
                        "Example User",
                        true,
                        new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 8, 30, 11, 0, 0, DateTimeKind.Utc),
                        [RoleNames.User, RoleNames.Admin])
                ],
                1,
                20,
                1,
                1,
                false,
                false));

        var roleService = new Mock<IAdminUserRoleService>();
        var managementService = new Mock<IAdminUserManagementService>();
        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);

        var result = await controller.GetUsers(null, null, null, 1, 20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminUserPageDto>(ok.Value);
        var user = Assert.Single(payload.Items);
        Assert.Equal("user@example.com", user.Email);
        Assert.Contains(RoleNames.Admin, user.Roles);
    }

    [Fact]
    public async Task GetRoles_ReturnsMappedRoles()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        queryService
            .Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AdminRoleResult(Guid.Parse("22222222-2222-2222-2222-222222222222"), RoleNames.User, "Default role"),
                new AdminRoleResult(Guid.Parse("33333333-3333-3333-3333-333333333333"), RoleNames.Admin, "Admin role")
            ]);

        var roleService = new Mock<IAdminUserRoleService>();
        var managementService = new Mock<IAdminUserManagementService>();
        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);

        var result = await controller.GetRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<AdminRoleDto>>(ok.Value);
        Assert.Equal(2, payload.Count);
        Assert.Contains(payload, x => x.Name == RoleNames.Admin);
    }

    [Fact]
    public async Task UpdateUserRoles_ReturnsBadRequest_WhenAdminRemovesOwnAdminRole()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        var roleService = new Mock<IAdminUserRoleService>(MockBehavior.Strict);
        var managementService = new Mock<IAdminUserManagementService>();
        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);
        var currentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        controller.ControllerContext = BuildControllerContext(currentUserId, "admin@example.com", [RoleNames.Admin]);

        var result = await controller.UpdateUserRoles(
            currentUserId,
            new UpdateAdminUserRolesRequest([RoleNames.User]),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("You cannot remove the Admin role from your own account.", badRequest.Value);
        roleService.Verify(x => x.UpdateRolesAsync(It.IsAny<UpdateAdminUserRolesCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserRoles_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        var roleService = new Mock<IAdminUserRoleService>();
        var managementService = new Mock<IAdminUserManagementService>();
        roleService
            .Setup(x => x.UpdateRolesAsync(It.IsAny<UpdateAdminUserRolesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminUserListItemResult?)null);

        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);
        controller.ControllerContext = BuildControllerContext(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "admin@example.com",
            [RoleNames.Admin]);

        var result = await controller.UpdateUserRoles(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new UpdateAdminUserRolesRequest([RoleNames.User, RoleNames.Admin]),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateUserRoles_ReturnsUpdatedUser_WhenValid()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        var roleService = new Mock<IAdminUserRoleService>();
        var managementService = new Mock<IAdminUserManagementService>();
        var targetUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        roleService
            .Setup(x => x.UpdateRolesAsync(
                It.Is<UpdateAdminUserRolesCommand>(command =>
                    command.UserId == targetUserId
                    && command.RoleNames.SequenceEqual(new[] { RoleNames.User, RoleNames.Admin })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUserListItemResult(
                targetUserId,
                "user@example.com",
                "Example User",
                true,
                new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 30, 11, 0, 0, DateTimeKind.Utc),
                [RoleNames.User, RoleNames.Admin]));

        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);
        controller.ControllerContext = BuildControllerContext(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "admin@example.com",
            [RoleNames.Admin]);

        var result = await controller.UpdateUserRoles(
            targetUserId,
            new UpdateAdminUserRolesRequest([RoleNames.User, RoleNames.Admin]),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminUserDto>(ok.Value);
        Assert.Equal("user@example.com", payload.Email);
        Assert.Contains(RoleNames.Admin, payload.Roles);
    }

    [Fact]
    public async Task GetUser_ReturnsMappedDetails_WhenUserExists()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        var roleService = new Mock<IAdminUserRoleService>();
        var managementService = new Mock<IAdminUserManagementService>();
        var targetUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        managementService
            .Setup(x => x.GetUserByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUserDetailsResult(
                targetUserId,
                "user@example.com",
                "Example User",
                "google-subject-123",
                true,
                new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 30, 11, 0, 0, DateTimeKind.Utc),
                [RoleNames.User, RoleNames.Admin]));

        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);

        var result = await controller.GetUser(targetUserId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminUserDetailsDto>(ok.Value);
        Assert.Equal("google-subject-123", payload.GoogleSubjectId);
        Assert.Contains(RoleNames.Admin, payload.Roles);
    }

    [Fact]
    public async Task BlockUser_ReturnsBadRequest_WhenAdminBlocksOwnAccount()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        var roleService = new Mock<IAdminUserRoleService>();
        var managementService = new Mock<IAdminUserManagementService>(MockBehavior.Strict);
        var currentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);
        controller.ControllerContext = BuildControllerContext(currentUserId, "admin@example.com", [RoleNames.Admin]);

        var result = await controller.BlockUser(currentUserId, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("You cannot block your own account.", badRequest.Value);
        managementService.Verify(x => x.SetBlockedAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BlockUser_ReturnsUpdatedDetails_WhenValid()
    {
        var queryService = new Mock<IAdminUserQueryService>();
        var roleService = new Mock<IAdminUserRoleService>();
        var managementService = new Mock<IAdminUserManagementService>();
        var currentUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var targetUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        managementService
            .Setup(x => x.SetBlockedAsync(targetUserId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminUserDetailsResult(
                targetUserId,
                "user@example.com",
                "Example User",
                "google-subject-123",
                false,
                new DateTime(2026, 8, 30, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 30, 11, 0, 0, DateTimeKind.Utc),
                [RoleNames.User]));

        var controller = new AdminController(queryService.Object, roleService.Object, managementService.Object);
        controller.ControllerContext = BuildControllerContext(currentUserId, "admin@example.com", [RoleNames.Admin]);

        var result = await controller.BlockUser(targetUserId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AdminUserDetailsDto>(ok.Value);
        Assert.False(payload.IsActive);
    }

    private static ControllerContext BuildControllerContext(Guid userId, string email, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }
}
