using DomainCopilot.Application.Adjudication;
using DomainCopilot.Application.Tests.CaseData;
using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Application.Tests.Adjudication;

public class LookupClaimHistoryToolExecutorTests
{
    private static ClaimHistoryRecord Claim(string claimNumber, string policyNumber, DateOnly dateOfLoss) =>
        ClaimHistoryRecord.Create(claimNumber, policyNumber, dateOfLoss, ClaimLossType.Collision, "d", 1000m, null, false, null);

    [Fact]
    public async Task Execute_FindsClaimWithinDefaultWindow()
    {
        var repo = new FakeClaimHistoryRepository();
        await repo.AddAsync(Claim("CLM-OTHER", "MMIC-PAP-1", new DateOnly(2025, 8, 1)));
        await repo.SaveChangesAsync();
        var executor = new LookupClaimHistoryToolExecutor(repo);

        var result = await executor.ExecuteAsync("""{"policyNumber": "MMIC-PAP-1", "referenceDateOfLoss": "2025-08-03"}""");

        Assert.True(result.Success);
        Assert.Contains("CLM-OTHER", result.ResultJson);
        Assert.Contains("\"duplicateClaimsFound\":1", result.ResultJson);
    }

    [Fact]
    public async Task Execute_ExcludesTheCurrentClaimItself()
    {
        var repo = new FakeClaimHistoryRepository();
        await repo.AddAsync(Claim("CLM-CURRENT", "MMIC-PAP-1", new DateOnly(2025, 8, 3)));
        await repo.SaveChangesAsync();
        var executor = new LookupClaimHistoryToolExecutor(repo);

        var result = await executor.ExecuteAsync(
            """{"policyNumber": "MMIC-PAP-1", "referenceDateOfLoss": "2025-08-03", "excludeClaimNumber": "CLM-CURRENT"}""");

        Assert.True(result.Success);
        Assert.Contains("\"duplicateClaimsFound\":0", result.ResultJson);
    }

    [Fact]
    public async Task Execute_RespectsCustomWindowDays()
    {
        var repo = new FakeClaimHistoryRepository();
        await repo.AddAsync(Claim("CLM-FAR", "MMIC-PAP-1", new DateOnly(2025, 8, 3).AddDays(45)));
        await repo.SaveChangesAsync();
        var executor = new LookupClaimHistoryToolExecutor(repo);

        var result = await executor.ExecuteAsync(
            """{"policyNumber": "MMIC-PAP-1", "referenceDateOfLoss": "2025-08-03", "windowDays": 30}""");

        Assert.True(result.Success);
        Assert.Contains("\"duplicateClaimsFound\":0", result.ResultJson);
    }

    [Fact]
    public async Task Execute_InvalidDate_FailsWithClearMessage()
    {
        var executor = new LookupClaimHistoryToolExecutor(new FakeClaimHistoryRepository());

        var result = await executor.ExecuteAsync("""{"policyNumber": "MMIC-PAP-1", "referenceDateOfLoss": "not-a-date"}""");

        Assert.False(result.Success);
        Assert.Contains("referenceDateOfLoss", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_MissingRequiredArgument_Fails()
    {
        var executor = new LookupClaimHistoryToolExecutor(new FakeClaimHistoryRepository());

        var result = await executor.ExecuteAsync("""{"policyNumber": "MMIC-PAP-1"}""");

        Assert.False(result.Success);
        Assert.Contains("referenceDateOfLoss", result.ErrorMessage);
    }
}
