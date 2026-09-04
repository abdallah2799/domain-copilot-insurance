using DomainCopilot.Domain.CaseData;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.CaseData;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DomainCopilot.Integration.Tests;

/// <summary>
/// Runs against a real, ephemeral MSSQL container — specifically to prove the Endorsements
/// list-as-JSON-column conversion (PolicyDeclarationConfiguration) round-trips correctly through
/// real SQL Server, which an in-memory provider wouldn't meaningfully exercise, and that the
/// PolicyNumber uniqueness is enforced by the database, not just this DbContext's change tracker.
/// </summary>
public sealed class PolicyDeclarationRepositoryTests : IAsyncLifetime
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

    private static PolicyDeclaration NewDeclaration(string policyNumber = "MMIC-PAP-100234") => PolicyDeclaration.Create(
        policyNumber, "John A. Whitfield", 2021, "Honda", "Accord", "1HGCV1F34MA012345",
        "PAP-2024-STD", new DateOnly(2024, 3, 1), 100_000m, 300_000m, 50_000m, 5_000m, 100_000m, 300_000m,
        true, 500m, true, 250m, 30m, ["Roadside Assistance Endorsement (END-RA-01)", "Custom Equipment Endorsement (END-CE-01)"]);

    [Fact]
    public async Task AddAndSave_ThenFindByPolicyNumber_RoundTripsEndorsementsList()
    {
        var repo = new PolicyDeclarationRepository(_dbContext);
        await repo.AddAsync(NewDeclaration());
        await repo.SaveChangesAsync();

        var reloaded = await repo.FindByPolicyNumberAsync("MMIC-PAP-100234");

        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.Endorsements.Count);
        Assert.Contains("Roadside Assistance Endorsement (END-RA-01)", reloaded.Endorsements);
    }

    [Fact]
    public async Task FindByPolicyNumber_WhenNotLoaded_ReturnsNull()
    {
        var repo = new PolicyDeclarationRepository(_dbContext);

        var result = await repo.FindByPolicyNumberAsync("does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task PolicyNumber_IsEnforcedUniqueBySqlServer_NotJustByConvention()
    {
        var repo = new PolicyDeclarationRepository(_dbContext);
        await repo.AddAsync(NewDeclaration("MMIC-PAP-DUP"));
        await repo.SaveChangesAsync();

        var options = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        await using var otherContext = new DomainCopilotDbContext(options);
        var otherRepo = new PolicyDeclarationRepository(otherContext);
        await otherRepo.AddAsync(NewDeclaration("MMIC-PAP-DUP"));

        await Assert.ThrowsAsync<DbUpdateException>(() => otherRepo.SaveChangesAsync());
    }
}
