namespace LegalAssistant.Api.Services.Auth;

public interface IRefreshTokenService
{
    Task<AuthTokensResult> IssueTokensAsync(AuthenticatedUser user, CancellationToken cancellationToken);
    Task<AuthTokensResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
