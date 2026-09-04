using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class StandardPayoutToolExecutorTests
{
    private readonly StandardPayoutToolExecutor _executor = new();

    [Fact]
    public void Execute_ValidArguments_ReturnsComputedPayout()
    {
        var result = _executor.Execute("""{"estimatedDamage": 3000, "applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.True(result.Success);
        Assert.Contains("2500", result.ResultJson);
    }

    [Fact]
    public void Execute_GlassWaiverTrue_IgnoresDeductible()
    {
        var result = _executor.Execute("""{"estimatedDamage": 1200, "applicableLimit": 50000, "applicableDeductible": 500, "glassOnlyDeductibleWaiverApplies": true}""");

        Assert.True(result.Success);
        Assert.Contains("1200", result.ResultJson);
    }

    [Fact]
    public void Execute_MissingRequiredArgument_FailsRatherThanDefaultingToZero()
    {
        var result = _executor.Execute("""{"applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.False(result.Success);
        Assert.Contains("estimatedDamage", result.ErrorMessage);
    }

    [Fact]
    public void Execute_MalformedJson_Fails()
    {
        var result = _executor.Execute("{not valid json");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Execute_NegativeDamage_FailsRatherThanThrowing()
    {
        var result = _executor.Execute("""{"estimatedDamage": -100, "applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Execute_WrongArgumentType_Fails()
    {
        var result = _executor.Execute("""{"estimatedDamage": "not a number", "applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.False(result.Success);
    }

    [Fact]
    public void Definition_HasExpectedToolName()
    {
        Assert.Equal("calculate_standard_payout", _executor.Definition.Name);
    }
}
