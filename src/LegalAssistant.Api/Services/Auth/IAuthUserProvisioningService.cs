namespace LegalAssistant.Api.Services.Auth;

public interface IAuthUserProvisioningService
{
    Task<AuthenticatedUser> ProvisionGoogleUserAsync(GoogleUserInfo googleUser, CancellationToken cancellationToken);
}
