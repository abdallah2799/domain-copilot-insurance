using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DomainCopilot.Domain.Identity;
using DomainCopilot.Infrastructure.Identity;

namespace DomainCopilot.Contract.Tests;

public class JwtTokenServiceTests
{
    private static AuthOptions CreateOptions() => new()
    {
        JwtSigningKey = "unit-test-signing-key-at-least-256-bits-long-for-hs256",
        JwtIssuer = "domain-copilot-tests",
        JwtAudience = "domain-copilot-tests-clients",
        TokenExpiryMinutes = 60,
    };

    [Fact]
    public void GenerateToken_IncludesUsernameAndRoleClaims()
    {
        var sut = new JwtTokenService(CreateOptions());
        var user = User.Create("adjuster", "hash", UserRole.Adjuster);

        var token = sut.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("adjuster", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Adjuster", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("domain-copilot-tests", jwt.Issuer);
        Assert.Contains("domain-copilot-tests-clients", jwt.Audiences);
    }

    [Fact]
    public void GenerateToken_WithoutASigningKeyConfigured_Throws()
    {
        var options = CreateOptions();
        options.JwtSigningKey = "";
        var sut = new JwtTokenService(options);

        Assert.Throws<InvalidOperationException>(() => sut.GenerateToken(User.Create("analyst", "hash", UserRole.Analyst)));
    }

    [Fact]
    public void GenerateToken_ForDifferentRoles_ProducesDifferentRoleClaims()
    {
        var sut = new JwtTokenService(CreateOptions());

        var adjusterToken = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerateToken(User.Create("a", "hash", UserRole.Adjuster)));
        var analystToken = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerateToken(User.Create("b", "hash", UserRole.Analyst)));

        Assert.Equal("Adjuster", adjusterToken.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("Analyst", analystToken.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }
}
