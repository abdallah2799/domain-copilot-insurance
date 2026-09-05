namespace DomainCopilot.Application.Identity;

public sealed record LoginResult(string Token, string Username, string Role);

/// <summary>FR-8's login use case: verify credentials against the hashed value on record, then issue
/// a token carrying the user's role as a claim (the server-side authorization enforcement in Api's
/// controllers is what actually restricts anything — this token is only ever a claim carrier, never
/// itself trusted for authorization decisions beyond what its signature already proves).</summary>
public sealed class AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
{
    public async Task<LoginResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByUsernameAsync(username, cancellationToken);
        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return new LoginResult(tokenService.GenerateToken(user), user.Username, user.Role.ToString());
    }
}
