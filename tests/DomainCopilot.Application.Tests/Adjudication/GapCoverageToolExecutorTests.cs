using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class GapCoverageToolExecutorTests
{
    private readonly GapCoverageToolExecutor _executor = new();

    [Fact]
    public async Task Execute_GapWithinLimit_ReturnsFullGap()
    {
        var result = await _executor.ExecuteAsync("""{"loanOrLeaseBalance": 22000, "totalLossSettlement": 18000, "endorsementLimit": 10000}""");

        Assert.True(result.Success);
        Assert.Contains("4000", result.ResultJson);
    }

    [Fact]
    public async Task Execute_GapExceedsLimit_CapsAtLimit()
    {
        var result = await _executor.ExecuteAsync("""{"loanOrLeaseBalance": 30000, "totalLossSettlement": 18000, "endorsementLimit": 5000}""");

        Assert.True(result.Success);
        Assert.Contains("5000", result.ResultJson);
    }

    [Fact]
    public async Task Execute_SettlementExceedsBalance_ReturnsZero()
    {
        var result = await _executor.ExecuteAsync("""{"loanOrLeaseBalance": 15000, "totalLossSettlement": 18000, "endorsementLimit": 10000}""");

        Assert.True(result.Success);
        Assert.Contains("0", result.ResultJson);
    }

    [Fact]
    public async Task Execute_MissingRequiredArgument_Fails()
    {
        var result = await _executor.ExecuteAsync("""{"loanOrLeaseBalance": 22000, "totalLossSettlement": 18000}""");

        Assert.False(result.Success);
        Assert.Contains("endorsementLimit", result.ErrorMessage);
    }
}
