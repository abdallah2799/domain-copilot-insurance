using DomainCopilot.Application.Identity;
using DomainCopilot.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.Identity;

public sealed class UserRepository(DomainCopilotDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
