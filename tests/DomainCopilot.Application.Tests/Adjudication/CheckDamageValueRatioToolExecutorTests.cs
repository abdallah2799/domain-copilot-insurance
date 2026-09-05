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
}
