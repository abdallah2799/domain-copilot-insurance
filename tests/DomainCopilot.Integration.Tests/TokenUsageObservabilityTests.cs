using DomainCopilot.Application.Observability;
using DomainCopilot.Infrastructure.Observability;
using DomainCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace DomainCopilot.Integration.Tests;

/// <summary>Runs against a real, ephemeral MSSQL container -- specifically to prove
/// <see cref="EfTokenUsageRecorder"/> genuinely persists (not just tracks in memory) and that the
/// query service's aggregate totals are computed from real rows, not just the recent-entries page.</summary>
public sealed class TokenUsageObservabilityTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private IDbContextFactory<DomainCopilotDbContext> _dbContextFactory = null!;
    private DomainCopilotDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<DomainCopilotDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _dbContext = new DomainCopilotDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
        _dbContextFactory = new FixedOptionsDbContextFactory(options);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _container.DisposeAsync();
    }

    private sealed class FixedOptionsDbContextFactory(DbContextOptions<DomainCopilotDbContext> options) : IDbContextFactory<DomainCopilotDbContext>
    {
        public DomainCopilotDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task RecordAsync_ThenQueried_RoundTripsWithARealEstimatedCost()
    {
        var pricing = new ModelPricingOptions { Prices = { ["gpt-4o-mini"] = new ModelPrice(0.15m, 0.60m) } };
        var recorder = new EfTokenUsageRecorder(_dbContextFactory, pricing);

        await recorder.RecordAsync(new TokenUsageEntry("trace-1", "CoverageMatcher", "OpenAI", "gpt-4o-mini", PromptTokens: 1_000_000, CompletionTokens: 500_000));

        var queryService = new EfTokenUsageQueryService(_dbContext);
        var report = await queryService.GetReportAsync();

        var recorded = Assert.Single(report.RecentEntries);
        Assert.Equal("trace-1", recorded.CorrelationId);
        Assert.Equal(0.15m + 0.30m, recorded.EstimatedCostUsd);
        Assert.Equal(1_000_000, report.TotalPromptTokens);
        Assert.Equal(500_000, report.TotalCompletionTokens);
        Assert.Equal(0.45m, report.TotalEstimatedCostUsd);
    }

    [Fact]
    public async Task GetReportAsync_Totals_SumAcrossMultipleRecordedCalls_NotJustTheRecentPage()
    {
        var pricing = new ModelPricingOptions();
        var recorder = new EfTokenUsageRecorder(_dbContextFactory, pricing);

        await recorder.RecordAsync(new TokenUsageEntry("trace-a", "CoverageMatcher", "Ollama", "llama3.1", 100, 20));
        await recorder.RecordAsync(new TokenUsageEntry("trace-b", "AnomalyAnalyst", "Ollama", "llama3.1", 200, 40));

        var queryService = new EfTokenUsageQueryService(_dbContext);
        var report = await queryService.GetReportAsync(recentLimit: 1);

        Assert.Single(report.RecentEntries);
        Assert.Equal(300, report.TotalPromptTokens);
        Assert.Equal(60, report.TotalCompletionTokens);
    }

    [Fact]
    public async Task GetReportAsync_WithNoRecordsYet_ReturnsZeroTotalsRatherThanThrowing()
    {
        var queryService = new EfTokenUsageQueryService(_dbContext);

        var report = await queryService.GetReportAsync();

        Assert.Empty(report.RecentEntries);
        Assert.Equal(0, report.TotalPromptTokens);
        Assert.Equal(0, report.TotalCompletionTokens);
        Assert.Equal(0m, report.TotalEstimatedCostUsd);
    }
}
