using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class TotalLossSettlementToolExecutorTests
{
    private readonly TotalLossSettlementToolExecutor _executor = new();

    [Fact]
    public async Task Execute_SurrenderScenario_ComputesSettlement()
    {
        var result = await _executor.ExecuteAsync("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 900}""");

        Assert.True(result.Success);
        Assert.Contains("18400", result.ResultJson);
    }

    [Fact]
    public async Task Execute_RetainedSalvage_DeductsSalvageValue()
    {
        var result = await _executor.ExecuteAsync("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 900, "salvageValueIfRetained": 4000}""");

        Assert.True(result.Success);
        Assert.Contains("14400", result.ResultJson);
    }

    [Fact]
    public async Task Execute_MissingRequiredArgument_Fails()
    {
        var result = await _executor.ExecuteAsync("""{"applicableDeductible": 500, "salesTaxAndFees": 900}""");

        Assert.False(result.Success);
        Assert.Contains("actualCashValue", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_OmittedOptionalSalvageValue_DefaultsToZero()
    {
        var withSalvage = await _executor.ExecuteAsync("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 0, "salvageValueIfRetained": 0}""");
        var withoutSalvage = await _executor.ExecuteAsync("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 0}""");

        Assert.Equal(withSalvage.ResultJson, withoutSalvage.ResultJson);
    }
}
