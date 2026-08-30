namespace LegalAssistant.Api.Services.Auth;

public sealed class UserBlockedException : Exception
{
    public UserBlockedException()
        : base("User account is blocked.")
    {
    }
}
