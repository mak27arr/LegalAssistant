using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LegalAssistant.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LegalAssistant.Api.Services.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IOptions<AuthOptions> _authOptions;

    public JwtTokenService(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions;
    }

    public AccessTokenResult CreateAccessToken(AuthenticatedUser user)
    {
        var jwtOptions = _authOptions.Value.Jwt;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, jwtOptions.AccessTokenLifetimeMinutes));
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(ClaimTypes.Name, user.FullName)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}
