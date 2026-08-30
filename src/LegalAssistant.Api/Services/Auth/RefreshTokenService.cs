using System.Security.Cryptography;
using System.Text;
using LegalAssistant.Api.Configuration;
using LegalAssistant.Domain.Models;
using LegalAssistant.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LegalAssistant.Api.Services.Auth;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly LegalAssistantDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOptions<AuthOptions> _authOptions;

    public RefreshTokenService(
        LegalAssistantDbContext db,
        IJwtTokenService jwtTokenService,
        IOptions<AuthOptions> authOptions)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _authOptions = authOptions;
    }

    public async Task<AuthTokensResult> IssueTokensAsync(AuthenticatedUser user, CancellationToken cancellationToken)
    {
        var accessToken = _jwtTokenService.CreateAccessToken(user);
        var refreshTokenValue = GenerateRefreshToken();
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(Math.Max(1, _authOptions.Value.RefreshToken.LifetimeDays));

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = ComputeHash(refreshTokenValue),
            ExpiresAt = refreshTokenExpiresAt,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthTokensResult(
            accessToken.AccessToken,
            accessToken.ExpiresAtUtc,
            refreshTokenValue,
            refreshTokenExpiresAt);
    }

    public async Task<AuthTokensResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hashedToken = ComputeHash(refreshToken);
        var storedToken = await _db.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.TokenHash == hashedToken, cancellationToken);

        if (storedToken == null || storedToken.RevokedAt != null || storedToken.ExpiresAt <= DateTime.UtcNow || !storedToken.User.IsActive)
        {
            return null;
        }

        var roles = storedToken.User.UserRoles.Select(x => x.Role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var user = new AuthenticatedUser(storedToken.User.Id, storedToken.User.Email, storedToken.User.FullName, roles);
        var accessToken = _jwtTokenService.CreateAccessToken(user);

        if (!_authOptions.Value.RefreshToken.RotateOnUse)
        {
            return new AuthTokensResult(
                accessToken.AccessToken,
                accessToken.ExpiresAtUtc,
                refreshToken,
                storedToken.ExpiresAt);
        }

        var replacementValue = GenerateRefreshToken();
        var replacementHash = ComputeHash(replacementValue);
        var replacementExpiresAt = DateTime.UtcNow.AddDays(Math.Max(1, _authOptions.Value.RefreshToken.LifetimeDays));

        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReplacedByTokenHash = replacementHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            TokenHash = replacementHash,
            ExpiresAt = replacementExpiresAt,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthTokensResult(
            accessToken.AccessToken,
            accessToken.ExpiresAtUtc,
            replacementValue,
            replacementExpiresAt);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hashedToken = ComputeHash(refreshToken);
        var storedToken = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hashedToken, cancellationToken);
        if (storedToken == null || storedToken.RevokedAt != null)
        {
            return;
        }

        storedToken.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
