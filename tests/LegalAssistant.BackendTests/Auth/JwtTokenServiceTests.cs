using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LegalAssistant.Api.Configuration;
using LegalAssistant.Api.Services.Auth;
using LegalAssistant.Application.Admin;
using Microsoft.Extensions.Options;

namespace LegalAssistant.BackendTests.Auth;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ProducesExpectedClaims()
    {
        var options = Options.Create(new AuthOptions
        {
            Jwt = new JwtAuthOptions
            {
                Issuer = "LegalAssistant.Api",
                Audience = "LegalAssistant.Frontend",
                SigningKey = "test-signing-key-with-sufficient-length-1234567890",
                AccessTokenLifetimeMinutes = 15
            }
        });

        var service = new JwtTokenService(options);
        var user = new AuthenticatedUser(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "user@example.com",
            "Example User",
            [RoleNames.User, RoleNames.Admin]);

        var result = service.CreateAccessToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal("LegalAssistant.Api", token.Issuer);
        Assert.Contains("LegalAssistant.Frontend", token.Audiences);
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == user.Id.ToString());
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Email && claim.Value == user.Email);
        Assert.Contains(token.Claims, claim => claim.Type == ClaimTypes.Name && claim.Value == user.FullName);

        var roleClaims = token.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        Assert.Contains(RoleNames.User, roleClaims);
        Assert.Contains(RoleNames.Admin, roleClaims);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
    }
}
