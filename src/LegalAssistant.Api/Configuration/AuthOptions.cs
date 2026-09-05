namespace LegalAssistant.Api.Configuration;

public sealed class AuthOptions
{
    public const string SectionName = "Authentication";

    public string PublicApiBaseUrl { get; set; } = string.Empty;
    public GoogleAuthOptions Google { get; set; } = new();
    public JwtAuthOptions Jwt { get; set; } = new();
    public RefreshTokenOptions RefreshToken { get; set; } = new();
    public SessionAuthOptions Session { get; set; } = new();
    public FrontendAuthOptions Frontend { get; set; } = new();
    public BootstrapAuthOptions Bootstrap { get; set; } = new();
}

public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-google";
    public string LoginPath { get; set; } = "/api/auth/google/login";
}

public sealed class JwtAuthOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}

public sealed class RefreshTokenOptions
{
    public int LifetimeDays { get; set; } = 30;
    public string CookieName { get; set; } = "legalassistant.refresh";
    public bool RotateOnUse { get; set; } = true;
}

public sealed class SessionAuthOptions
{
    public string CookieName { get; set; } = "__Host-legalassistant.session";
    public int IdleTimeoutMinutes { get; set; } = 60;
    public int AbsoluteLifetimeHours { get; set; } = 12;
}

public sealed class FrontendAuthOptions
{
    public string SuccessRedirectUrl { get; set; } = string.Empty;
    public string FailureRedirectUrl { get; set; } = string.Empty;
}

public sealed class BootstrapAuthOptions
{
    public string[] AdminEmails { get; set; } = [];
}
