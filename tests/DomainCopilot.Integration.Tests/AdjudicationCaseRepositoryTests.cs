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
        var acase = AdjudicationCase.Create("CLM-2025-04417", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), "test-user");
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
    public async Task ListByCreatedBy_ReturnsOnlyThatUsersOwnCases()
    {
        var repo = new AdjudicationCaseRepository(_dbContext);
        var analystOneCase = AdjudicationCase.Create("CLM-OWNER-1", "MMIC-PAP-1", new DateOnly(2025, 8, 3), "analyst.one");
        var analystTwoCase = AdjudicationCase.Create("CLM-OWNER-2", "MMIC-PAP-1", new DateOnly(2025, 8, 3), "analyst.two");

        await repo.AddAsync(analystOneCase);
        await repo.AddAsync(analystTwoCase);
        await repo.SaveChangesAsync();

        var analystOnesCases = await repo.ListByCreatedByAsync("analyst.one");

        Assert.Single(analystOnesCases);
        Assert.Equal("CLM-OWNER-1", analystOnesCases[0].ClaimNumber);
    }

    [Fact]
    public async Task SameClaimNumber_CanHaveMultipleRuns_NotUniqueConstrained()
    {
        var repo = new AdjudicationCaseRepository(_dbContext);
        var firstRun = AdjudicationCase.Create("CLM-REOPEN-1", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), "test-user");
        var secondRun = AdjudicationCase.Create("CLM-REOPEN-1", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), "test-user");

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
        var acase = AdjudicationCase.Create("CLM-2025-04999", "MMIC-PAP-999", new DateOnly(2025, 8, 3), "test-user");
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

    /// <summary>
    /// Reproduces exactly what the progress stream does: one long-lived scope re-reading a row in a
    /// loop while a different scope (the background pipeline) advances it.
    ///
    /// A tracked read resolves through EF Core's identity map and keeps returning the instance
    /// loaded the first time, so the stream compared its opening snapshot against itself for the
    /// whole run and reported no progress -- the page only updated when navigating away built a new
    /// scope. This pins the distinction so the stream cannot silently go blind again.
    /// </summary>
    [Fact]
    public async Task ARowChangedByAnotherScope_IsInvisibleToATrackedRead_ButSeenByFindByIdForRead()
    {
        var repository = new AdjudicationCaseRepository(_dbContext);
        var adjudicationCase = AdjudicationCase.Create("CLM-STREAM-1", "POL-STREAM-1", new DateOnly(2025, 8, 3), "adjuster");
        await repository.AddAsync(adjudicationCase);
        await repository.SaveChangesAsync();

        // The stream's opening read, which puts the entity into this context's identity map.
        var opening = await repository.FindByIdAsync(adjudicationCase.Id);
        Assert.Equal(AdjudicationRunStatus.Pending, opening!.Status);

        // A different scope advances the run, exactly as the background pipeline does.
        var writerOptions = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        await using (var writerContext = new DomainCopilotDbContext(writerOptions))
        {
            var writerRepository = new AdjudicationCaseRepository(writerContext);
            var fromWriter = await writerRepository.FindByIdAsync(adjudicationCase.Id);
            fromWriter!.BeginCoverageMatching();
            await writerRepository.SaveChangesAsync();
        }

        // The trap: still Pending, because the identity map wins over what the database now says.
        var trackedReread = await repository.FindByIdAsync(adjudicationCase.Id);
        Assert.Equal(AdjudicationRunStatus.Pending, trackedReread!.Status);

        // The fix the stream depends on.
        var untrackedReread = await repository.FindByIdForReadAsync(adjudicationCase.Id);
        Assert.Equal(AdjudicationRunStatus.MatchingCoverage, untrackedReread!.Status);
    }
}
