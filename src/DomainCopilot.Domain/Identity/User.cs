namespace DomainCopilot.Domain.Identity;

/// <summary>
/// A person who can sign in to Domain Copilot. Password hashing itself is an Infrastructure
/// concern (ADR-0012) — this entity only ever stores and compares an already-hashed value, never a
/// plaintext password, so a leaked database row is never a leaked credential on its own.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private User()
    {
        // EF Core materialization only — public construction goes through Create.
    }

    public static User Create(string username, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("A user must have a username.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("A user must have a password hash.", nameof(passwordHash));
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
