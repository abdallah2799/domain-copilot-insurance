namespace DomainCopilot.Application.Identity;

/// <summary>Port over password hashing/verification. Infrastructure provides the concrete
/// implementation — Application never chooses or references a specific hashing algorithm.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
