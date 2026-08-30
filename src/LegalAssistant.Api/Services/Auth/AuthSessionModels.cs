using System.Security.Claims;

namespace LegalAssistant.Api.Services.Auth;

public sealed record GoogleUserInfo(
    string Subject,
    string Email,
    string FullName);

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

public sealed record AccessTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);

public sealed record AuthTokensResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);

public sealed record RefreshTokenValidationResult(
    LegalAssistant.Domain.Models.RefreshToken RefreshToken,
    LegalAssistant.Domain.Models.User User,
    IReadOnlyList<string> Roles);

public static class ClaimsPrincipalExtensions
{
    public static AuthenticatedUser ToAuthenticatedUser(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("Authenticated user id claim is missing.");

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return new AuthenticatedUser(Guid.Parse(userId), email, name, roles);
    }
}
