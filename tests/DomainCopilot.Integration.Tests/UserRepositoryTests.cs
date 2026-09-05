using DomainCopilot.Domain.Identity;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DomainCopilot.Integration.Tests;

/// <summary>Runs against a real, ephemeral MSSQL container -- specifically to prove the unique
/// username constraint is real (enforced by the database, not just application-level convention)
/// and that a hashed password round-trips through real SQL Server unchanged.</summary>
public sealed class UserRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private DomainCopilotDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _dbContext = new DomainCopilotDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task AddAndSave_ThenFindByUsername_RoundTripsTheHashedPassword()
    {
        var repo = new UserRepository(_dbContext);
        var user = User.Create("adjuster.jane", "pbkdf2-hash-value", UserRole.Adjuster);

        await repo.AddAsync(user);
        await repo.SaveChangesAsync();

        var reloaded = await repo.FindByUsernameAsync("adjuster.jane");

        Assert.NotNull(reloaded);
        Assert.Equal("pbkdf2-hash-value", reloaded!.PasswordHash);
        Assert.Equal(UserRole.Adjuster, reloaded.Role);
    }

    [Fact]
    public async Task FindByUsername_WhenNotFound_ReturnsNull()
    {
        var repo = new UserRepository(_dbContext);

        var result = await repo.FindByUsernameAsync("nobody");

        Assert.Null(result);
    }

    [Fact]
    public async Task AnyAsync_ReflectsWhetherAnyUserHasBeenSeeded()
    {
        var repo = new UserRepository(_dbContext);

        Assert.False(await repo.AnyAsync());

        await repo.AddAsync(User.Create("analyst.joe", "hash", UserRole.Analyst));
        await repo.SaveChangesAsync();

        Assert.True(await repo.AnyAsync());
    }

    [Fact]
    public async Task AddingASecondUser_WithADuplicateUsername_ViolatesTheRealUniqueConstraint()
    {
        var repo = new UserRepository(_dbContext);
        await repo.AddAsync(User.Create("duplicate", "hash-1", UserRole.Analyst));
        await repo.SaveChangesAsync();

        await repo.AddAsync(User.Create("duplicate", "hash-2", UserRole.Adjuster));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => repo.SaveChangesAsync());
    }
}
