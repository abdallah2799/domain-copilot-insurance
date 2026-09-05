using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DomainCopilot.Application.Identity;
using DomainCopilot.Domain.Identity;
using Microsoft.IdentityModel.Tokens;

namespace DomainCopilot.Infrastructure.Identity;

/// <summary>ADR-0012's token issuer. The role claim uses the standard <see
/// cref="ClaimTypes.Role"/> type so ASP.NET Core's own <c>[Authorize(Roles = ...)]</c> checks it
/// automatically — no custom claims-transformation needed in Api.</summary>
public sealed class JwtTokenService(AuthOptions options) : ITokenService
{
    public string GenerateToken(User user)
    {
        if (string.IsNullOrWhiteSpace(options.JwtSigningKey))
        {
            throw new InvalidOperationException(
                "JWT_SIGNING_KEY is not configured -- set it in .env (see .env.example) before issuing tokens.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.TokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
