using DomainCopilot.Application.Identity;
using DomainCopilot.Domain.Identity;

namespace DomainCopilot.Application.Tests.Identity;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<string, User> _byUsername = new();

    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byUsername.GetValueOrDefault(username));

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_byUsername.Count > 0);

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _byUsername[user.Username] = user;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
