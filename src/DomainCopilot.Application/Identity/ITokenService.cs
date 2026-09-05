using DomainCopilot.Domain.Identity;

namespace DomainCopilot.Application.Identity;

/// <summary>Port over issuing a bearer token for an authenticated <see cref="User"/>. Infrastructure
/// provides the concrete JWT implementation — Application never references a token/crypto SDK.</summary>
public interface ITokenService
{
    string GenerateToken(User user);
}
