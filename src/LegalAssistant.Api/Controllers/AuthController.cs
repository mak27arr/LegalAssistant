using System.Security.Claims;
using LegalAssistant.Api.Configuration;
using LegalAssistant.Api.Dtos.Auth;
using LegalAssistant.Api.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string ExternalScheme = "External";
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IAuthUserProvisioningService _authUserProvisioningService;
    private readonly IRefreshTokenService _refreshTokenService;

    public AuthController(
        IOptions<AuthOptions> authOptions,
        IAuthUserProvisioningService authUserProvisioningService,
        IRefreshTokenService refreshTokenService)
    {
        _authOptions = authOptions;
        _authUserProvisioningService = authUserProvisioningService;
        _refreshTokenService = refreshTokenService;
    }

    [AllowAnonymous]
    [HttpGet("config")]
    public ActionResult<AuthConfigResponse> GetConfig()
    {
        var loginUrl = string.IsNullOrWhiteSpace(_authOptions.Value.PublicApiBaseUrl)
            ? null
            : $"{_authOptions.Value.PublicApiBaseUrl.TrimEnd('/')}/api/auth/google/login";

        return Ok(new AuthConfigResponse(
            new ProviderConfigResponse(
                new GoogleProviderConfigResponse(
                    Enabled: !string.IsNullOrWhiteSpace(_authOptions.Value.Google.ClientId),
                    LoginUrl: loginUrl))));
    }

    [AllowAnonymous]
    [HttpGet("google/login")]
    public IActionResult BeginGoogleLogin()
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.ActionLink(nameof(CompleteGoogleLogin), values: null, protocol: Request.Scheme, host: Request.Host.ToString())
        };

        return Challenge(props, "Google");
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public async Task<IActionResult> CompleteGoogleLogin(CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(ExternalScheme);
        if (!result.Succeeded || result.Principal == null)
        {
            return Redirect(BuildRedirectUrl(_authOptions.Value.Frontend.FailureRedirectUrl, "auth_status", "failed"));
        }

        var subject = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? result.Principal.FindFirstValue("sub");
        var email = result.Principal.FindFirstValue(ClaimTypes.Email) ?? result.Principal.FindFirstValue("email");
        var fullName = result.Principal.FindFirstValue(ClaimTypes.Name) ?? result.Principal.FindFirstValue("name");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
        {
            await HttpContext.SignOutAsync(ExternalScheme);
            return Redirect(BuildRedirectUrl(_authOptions.Value.Frontend.FailureRedirectUrl, "auth_status", "missing_claims"));
        }

        AuthenticatedUser user;
        try
        {
            user = await _authUserProvisioningService.ProvisionGoogleUserAsync(
                new GoogleUserInfo(subject, email, fullName),
                cancellationToken);
        }
        catch (UserBlockedException)
        {
            await HttpContext.SignOutAsync(ExternalScheme);
            return Redirect(BuildRedirectUrl(_authOptions.Value.Frontend.FailureRedirectUrl, "auth_status", "blocked"));
        }

        var tokens = await _refreshTokenService.IssueTokensAsync(user, cancellationToken);
        AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
        await HttpContext.SignOutAsync(ExternalScheme);

        return Redirect(BuildRedirectUrl(_authOptions.Value.Frontend.SuccessRedirectUrl, "auth_status", "success"));
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<AuthMeResponse> Me()
    {
        var user = User.ToAuthenticatedUser();
        return Ok(new AuthMeResponse(user.Id.ToString(), user.Email, user.FullName, user.Roles));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthRefreshResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshCookieName = _authOptions.Value.RefreshToken.CookieName;
        if (!Request.Cookies.TryGetValue(refreshCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        var tokens = await _refreshTokenService.RefreshAsync(refreshToken, cancellationToken);
        if (tokens == null)
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }

        AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
        return Ok(new AuthRefreshResponse(tokens.AccessToken, tokens.AccessTokenExpiresAtUtc));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshCookieName = _authOptions.Value.RefreshToken.CookieName;
        if (Request.Cookies.TryGetValue(refreshCookieName, out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
        {
            await _refreshTokenService.RevokeAsync(refreshToken, cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    private void AppendRefreshCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(_authOptions.Value.RefreshToken.CookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(expiresAtUtc)
        });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(_authOptions.Value.RefreshToken.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });
    }

    private static string BuildRedirectUrl(string baseUrl, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Frontend redirect URL is not configured.");
        }

        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{baseUrl}{separator}{key}={Uri.EscapeDataString(value)}";
    }
}
