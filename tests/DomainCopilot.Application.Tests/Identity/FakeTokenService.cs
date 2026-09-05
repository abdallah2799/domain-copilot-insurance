using DomainCopilot.Application.Identity;
using DomainCopilot.Domain.Identity;

namespace DomainCopilot.Application.Tests.Identity;

internal sealed class FakeTokenService : ITokenService
{
    public string GenerateToken(User user) => $"token-for:{user.Username}:{user.Role}";
}
