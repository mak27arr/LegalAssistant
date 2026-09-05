namespace LegalAssistant.Application.Auth;

public interface IUserSessionManager
{
    Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);
    Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
