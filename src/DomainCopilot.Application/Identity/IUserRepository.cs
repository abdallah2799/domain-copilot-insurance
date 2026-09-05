using DomainCopilot.Domain.Identity;

namespace DomainCopilot.Application.Identity;

/// <summary>Port over the relational store for users. Infrastructure provides the EF Core/MSSQL
/// implementation; Application never references EF Core directly.</summary>
public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
