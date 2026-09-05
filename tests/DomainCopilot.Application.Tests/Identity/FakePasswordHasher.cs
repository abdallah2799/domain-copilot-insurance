using DomainCopilot.Application.Identity;

namespace DomainCopilot.Application.Tests.Identity;

/// <summary>A trivially reversible "hash" (prefix marker, not real hashing) -- fine for exercising
/// AuthService's own logic, since the real algorithm is Infrastructure's concern and has its own
/// dedicated tests (Pbkdf2PasswordHasherTests).</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
}
