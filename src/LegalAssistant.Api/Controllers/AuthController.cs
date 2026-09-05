using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
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
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IAuthUserProvisioningService _authUserProvisioningService;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOptions<AuthOptions> authOptions,
        IAuthUserProvisioningService authUserProvisioningService,
        IAntiforgery antiforgery,
        ILogger<AuthController> logger)
    {
        _authOptions = authOptions;
        _authUserProvisioningService = authUserProvisioningService;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("config")]
    public ActionResult<AuthConfigResponse> GetConfig()
    {
        var loginPath = _authOptions.Value.Google.LoginPath;
        if (string.IsNullOrWhiteSpace(loginPath))
        {
            _logger.LogWarning("Google LoginPath is not configured in Authentication:Google options.");
        }

        var loginUrl = string.IsNullOrWhiteSpace(_authOptions.Value.PublicApiBaseUrl)
            ? loginPath
            : $"{_authOptions.Value.PublicApiBaseUrl.TrimEnd('/')}/{loginPath?.TrimStart('/')}";

        return Ok(new AuthConfigResponse(
            new ProviderConfigResponse(
                new GoogleProviderConfigResponse(
                    Enabled: !string.IsNullOrWhiteSpace(_authOptions.Value.Google.ClientId),
                    LoginUrl: loginUrl ?? string.Empty))));
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
        var result = await HttpContext.AuthenticateAsync(ApplicationAuthSchemes.External);
        if (!result.Succeeded || result.Principal == null)
        {
            return Redirect(BuildRedirectUrl(_authOptions.Value.Frontend.FailureRedirectUrl, "auth_status", "failed"));
        }

        var subject = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? result.Principal.FindFirstValue("sub");
        var email = result.Principal.FindFirstValue(ClaimTypes.Email) ?? result.Principal.FindFirstValue("email");
        var fullName = result.Principal.FindFirstValue(ClaimTypes.Name) ?? result.Principal.FindFirstValue("name");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
        {
            await HttpContext.SignOutAsync(ApplicationAuthSchemes.External);
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
            await HttpContext.SignOutAsync(ApplicationAuthSchemes.External);
            return Redirect(BuildRedirectUrl(_authOptions.Value.Frontend.FailureRedirectUrl, "auth_status", "blocked"));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ApplicationAuthSchemes.SessionIdClaimType, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, ApplicationAuthSchemes.Application);
        var now = DateTimeOffset.UtcNow;
        await HttpContext.SignInAsync(
            ApplicationAuthSchemes.Application,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                IssuedUtc = now,
                ExpiresUtc = now.AddMinutes(Math.Max(1, _authOptions.Value.Session.IdleTimeoutMinutes)),
                AllowRefresh = true
            });
        await HttpContext.SignOutAsync(ApplicationAuthSchemes.External);

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
    [HttpGet("csrf")]
    public ActionResult<AuthCsrfResponse> Csrf()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AuthCsrfResponse(tokens.RequestToken ?? string.Empty));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(ApplicationAuthSchemes.Application);
        return NoContent();
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
