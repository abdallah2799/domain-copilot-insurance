using DomainCopilot.Domain.Adjudication;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.Adjudication;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DomainCopilot.Integration.Tests;

/// <summary>
/// Runs against a real, ephemeral MSSQL container — specifically to prove the state machine's
/// transitions and JSON-blob fields persist and reload correctly through real SQL Server, and that
/// a claim number is genuinely not unique-constrained (a claim can be reopened into a second run).
/// </summary>
public sealed class AdjudicationCaseRepositoryTests : IAsyncLifetime
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
    public async Task AddAndSave_ThenFindById_RoundTripsStateAndJsonBlobs()
    {
        var repo = new AdjudicationCaseRepository(_dbContext);
        var acase = AdjudicationCase.Create("CLM-2025-04417", "MMIC-PAP-100234", new DateOnly(2025, 8, 3));
        acase.BeginCoverageMatching();
        acase.RecordCoverageMatch("""{"formVersion":"PAP-2024-STD"}""");

        await repo.AddAsync(acase);
        await repo.SaveChangesAsync();

        var reloaded = await repo.FindByIdAsync(acase.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(AdjudicationRunStatus.DetectingAnomalies, reloaded!.Status);
        Assert.Contains("PAP-2024-STD", reloaded.CoverageMatchResultJson);
    }

    [Fact]
    public async Task FindById_WhenNotFound_ReturnsNull()
    {
        var repo = new AdjudicationCaseRepository(_dbContext);

        var result = await repo.FindByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SameClaimNumber_CanHaveMultipleRuns_NotUniqueConstrained()
    {
        var repo = new AdjudicationCaseRepository(_dbContext);
        var firstRun = AdjudicationCase.Create("CLM-REOPEN-1", "MMIC-PAP-100234", new DateOnly(2025, 8, 3));
        var secondRun = AdjudicationCase.Create("CLM-REOPEN-1", "MMIC-PAP-100234", new DateOnly(2025, 8, 3));

        await repo.AddAsync(firstRun);
        await repo.AddAsync(secondRun);
        await repo.SaveChangesAsync();

        var all = await repo.ListAllAsync();
        Assert.Equal(2, all.Count(a => a.ClaimNumber == "CLM-REOPEN-1"));
    }

    [Fact]
    public async Task FullPipelineToApproval_PersistsAcrossReload()
    {
        var repo = new AdjudicationCaseRepository(_dbContext);
        var acase = AdjudicationCase.Create("CLM-2025-04999", "MMIC-PAP-999", new DateOnly(2025, 8, 3));
        acase.BeginCoverageMatching();
        acase.RecordCoverageMatch("{}");
        acase.RecordAnomalyFindings("{}");
        acase.RecordExclusionAnalysis("{}");
        acase.RecordRecommendation("""{"payout":2500}""");
        acase.Approve("adjuster.jane");

        await repo.AddAsync(acase);
        await repo.SaveChangesAsync();

        var options = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        await using var otherContext = new DomainCopilotDbContext(options);
        var otherRepo = new AdjudicationCaseRepository(otherContext);
        var reloaded = await otherRepo.FindByIdAsync(acase.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(AdjudicationRunStatus.Approved, reloaded!.Status);
        Assert.Equal("adjuster.jane", reloaded.ApprovedBy);
        Assert.NotNull(reloaded.ApprovedAtUtc);
    }
}
