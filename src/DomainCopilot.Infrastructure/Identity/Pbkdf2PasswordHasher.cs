using System.Security.Cryptography;
using DomainCopilot.Application.Identity;

namespace DomainCopilot.Infrastructure.Identity;

/// <summary>PBKDF2-HMAC-SHA256 password hashing (ADR-0012) — the BCL's own <see
/// cref="Rfc2898DeriveBytes"/>, not a third-party crypto library, so there is nothing new here to
/// track against a dependency advisory database. The iteration count is embedded in the stored hash
/// (not hardcoded at verify time) so a future increase to <see cref="Iterations"/> never breaks
/// verifying passwords hashed under the old count.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int SubkeySizeBytes = 32;

    // OWASP's current minimum recommendation for PBKDF2-HMAC-SHA256 (2023+ guidance).
    private const int Iterations = 210_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, SubkeySizeBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedSubkey = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedSubkey.Length);
        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
