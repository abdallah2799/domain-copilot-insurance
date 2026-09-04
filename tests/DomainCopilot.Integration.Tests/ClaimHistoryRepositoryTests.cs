using DomainCopilot.Domain.CaseData;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.CaseData;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DomainCopilot.Integration.Tests;

/// <summary>
/// Runs against a real, ephemeral MSSQL container — specifically to prove the date-window query
/// (FindByPolicyNumberWithinWindowAsync) translates correctly to real SQL Server and actually
/// excludes out-of-window claims, not just that the LINQ compiles.
/// </summary>
public sealed class ClaimHistoryRepositoryTests : IAsyncLifetime
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

    private static ClaimHistoryRecord NewClaim(string claimNumber, string policyNumber, DateOnly dateOfLoss) =>
        ClaimHistoryRecord.Create(claimNumber, policyNumber, dateOfLoss, ClaimLossType.Collision, "d", 1000m, null, false, null);

    [Fact]
    public async Task AddAndSave_ThenFindByClaimNumber_RoundTripsFields()
    {
        var repo = new ClaimHistoryRepository(_dbContext);
        await repo.AddAsync(NewClaim("CLM-1", "MMIC-PAP-100234", new DateOnly(2025, 8, 3)));
        await repo.SaveChangesAsync();

        var reloaded = await repo.FindByClaimNumberAsync("CLM-1");

        Assert.NotNull(reloaded);
        Assert.Equal("MMIC-PAP-100234", reloaded!.PolicyNumber);
        Assert.Equal(ClaimLossType.Collision, reloaded.LossType);
    }

    [Fact]
    public async Task FindWithinWindow_ExcludesClaimOutsideTheWindow_ButIncludesClaimJustInsideIt()
    {
        var repo = new ClaimHistoryRepository(_dbContext);
        var reference = new DateOnly(2025, 8, 3);

        await repo.AddAsync(NewClaim("CLM-IN", "MMIC-PAP-1", reference.AddDays(89)));
        await repo.AddAsync(NewClaim("CLM-OUT", "MMIC-PAP-1", reference.AddDays(91)));
        await repo.SaveChangesAsync();

        var results = await repo.FindByPolicyNumberWithinWindowAsync("MMIC-PAP-1", reference, windowDays: 90);

        Assert.Contains(results, c => c.ClaimNumber == "CLM-IN");
        Assert.DoesNotContain(results, c => c.ClaimNumber == "CLM-OUT");
    }

    [Fact]
    public async Task FindWithinWindow_ExcludesClaimsOnADifferentPolicy()
    {
        var repo = new ClaimHistoryRepository(_dbContext);
        var reference = new DateOnly(2025, 8, 3);

        await repo.AddAsync(NewClaim("CLM-OTHER-POLICY", "MMIC-PAP-2", reference));
        await repo.SaveChangesAsync();

        var results = await repo.FindByPolicyNumberWithinWindowAsync("MMIC-PAP-1", reference, windowDays: 90);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindWithinWindow_LooksBeforeAndAfterTheReferenceDate()
    {
        var repo = new ClaimHistoryRepository(_dbContext);
        var reference = new DateOnly(2025, 8, 3);

        await repo.AddAsync(NewClaim("CLM-BEFORE", "MMIC-PAP-1", reference.AddDays(-30)));
        await repo.SaveChangesAsync();

        var results = await repo.FindByPolicyNumberWithinWindowAsync("MMIC-PAP-1", reference, windowDays: 90);

        Assert.Contains(results, c => c.ClaimNumber == "CLM-BEFORE");
    }

    [Fact]
    public async Task ClaimNumber_IsEnforcedUniqueBySqlServer_NotJustByConvention()
    {
        var repo = new ClaimHistoryRepository(_dbContext);
        await repo.AddAsync(NewClaim("CLM-DUP", "MMIC-PAP-1", new DateOnly(2025, 8, 3)));
        await repo.SaveChangesAsync();

        var options = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        await using var otherContext = new DomainCopilotDbContext(options);
        var otherRepo = new ClaimHistoryRepository(otherContext);
        await otherRepo.AddAsync(NewClaim("CLM-DUP", "MMIC-PAP-9", new DateOnly(2025, 9, 1)));

        await Assert.ThrowsAsync<DbUpdateException>(() => otherRepo.SaveChangesAsync());
    }
}
