namespace LegalAssistant.Api.Services.Auth;

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(AuthenticatedUser user);
}
