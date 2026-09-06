using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class CheckDamageValueRatioToolExecutorTests
{
    private readonly CheckDamageValueRatioToolExecutor _executor = new();

    [Fact]
    public async Task Execute_DamageAboveThreshold_ReturnsTrue()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": 13000, "approximateVehicleValue": 20000}""");

        Assert.True(result.Success);
        Assert.Contains("true", result.ResultJson);
    }

    [Fact]
    public async Task Execute_DamageBelowThreshold_ReturnsFalse()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": 3000, "approximateVehicleValue": 20000}""");

        Assert.True(result.Success);
        Assert.Contains("false", result.ResultJson);
    }

    [Fact]
    public async Task Execute_ZeroVehicleValue_Fails()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": 3000, "approximateVehicleValue": 0}""");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Execute_MissingArgument_Fails()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": 3000}""");

        Assert.False(result.Success);
        Assert.Contains("approximateVehicleValue", result.ErrorMessage);
    }

    // Regression test for the defect that stopped the four-agent workflow from ever completing.
    // The Anomaly Analyst emitted these arguments as quoted strings, this tool rejected them with
    // "Argument 'estimatedDamage' must be a number", and the model retried the identical call until
    // it exhausted its iteration budget and failed the run. Quoting a number inside a tool call's
    // JSON-string arguments is a formatting choice models make freely, not a different value.
    [Theory]
    [InlineData("""{"estimatedDamage": "13000", "approximateVehicleValue": "20000"}""")]
    [InlineData("""{"estimatedDamage": "13000", "approximateVehicleValue": 20000}""")]
    [InlineData("""{"estimatedDamage": 13000, "approximateVehicleValue": "20000"}""")]
    public async Task Execute_NumbersSentAsQuotedStrings_IsAcceptedNotRejected(string argumentsJson)
    {
        var result = await _executor.ExecuteAsync(argumentsJson);

        Assert.True(result.Success, $"expected quoted numbers to be accepted, got: {result.ErrorMessage}");
        Assert.Contains("true", result.ResultJson);
    }

    [Fact]
    public async Task Execute_NonNumericString_StillFails()
    {
        var result = await _executor.ExecuteAsync("""{"estimatedDamage": "not a number", "approximateVehicleValue": 20000}""");

        Assert.False(result.Success);
        Assert.Contains("estimatedDamage", result.ErrorMessage);
    }
}
