using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class StandardPayoutToolExecutorTests
{
    private readonly StandardPayoutToolExecutor _executor = new();

    [Fact]
    public async Task Execute_ValidArguments_ReturnsComputedPayout()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": 3000, "applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.True(result.Success);
        Assert.Contains("2500", result.ResultJson);
    }

    [Fact]
    public async Task Execute_GlassWaiverTrue_IgnoresDeductible()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": 1200, "applicableLimit": 50000, "applicableDeductible": 500, "glassOnlyDeductibleWaiverApplies": true}""");

        Assert.True(result.Success);
        Assert.Contains("1200", result.ResultJson);
    }

    [Fact]
    public async Task Execute_MissingRequiredArgument_FailsRatherThanDefaultingToZero()
    {
        var result = await _executor.ExecuteAsync("""{"applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.False(result.Success);
        Assert.Contains("estimatedDamage", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_MalformedJson_Fails()
    {
        var result = await _executor.ExecuteAsync("{not valid json");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_NegativeDamage_FailsRatherThanThrowing()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": -100, "applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_WrongArgumentType_Fails()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": "not a number", "applicableLimit": 50000, "applicableDeductible": 500}""");

        Assert.False(result.Success);
    }

    [Fact]
    public void Definition_HasExpectedToolName()
    {
        Assert.Equal("calculate_standard_payout", _executor.Definition.Name);
    }
}
