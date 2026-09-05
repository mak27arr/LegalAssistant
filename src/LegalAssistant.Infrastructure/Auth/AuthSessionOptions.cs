namespace LegalAssistant.Infrastructure.Auth;

public sealed class AuthSessionOptions
{
    public int IdleTimeoutMinutes { get; set; } = 60;
    public int AbsoluteLifetimeHours { get; set; } = 12;
}
