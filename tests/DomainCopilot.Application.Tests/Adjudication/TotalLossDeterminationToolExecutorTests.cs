using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class TotalLossDeterminationToolExecutorTests
{
    private readonly TotalLossDeterminationToolExecutor _executor = new();

    [Fact]
    public void Execute_RepairPlusSalvageExceedsAcv_ReturnsTrue()
    {
        var result = _executor.Execute("""{"repairCost": 15000, "salvageValue": 6000, "actualCashValue": 20000}""");

        Assert.True(result.Success);
        Assert.Contains("true", result.ResultJson);
    }

    [Fact]
    public void Execute_BelowBothThresholds_ReturnsFalse()
    {
        var result = _executor.Execute("""{"repairCost": 5000, "salvageValue": 1000, "actualCashValue": 20000}""");

        Assert.True(result.Success);
        Assert.Contains("false", result.ResultJson);
    }

    [Fact]
    public void Execute_MissingRequiredArgument_Fails()
    {
        var result = _executor.Execute("""{"repairCost": 5000, "salvageValue": 1000}""");

        Assert.False(result.Success);
        Assert.Contains("actualCashValue", result.ErrorMessage);
    }

    [Fact]
    public void Execute_ZeroActualCashValue_FailsRatherThanThrowing()
    {
        var result = _executor.Execute("""{"repairCost": 5000, "salvageValue": 1000, "actualCashValue": 0}""");

        Assert.False(result.Success);
    }
}
