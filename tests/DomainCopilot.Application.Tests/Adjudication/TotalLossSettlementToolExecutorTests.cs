using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class TotalLossSettlementToolExecutorTests
{
    private readonly TotalLossSettlementToolExecutor _executor = new();

    [Fact]
    public void Execute_SurrenderScenario_ComputesSettlement()
    {
        var result = _executor.Execute("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 900}""");

        Assert.True(result.Success);
        Assert.Contains("18400", result.ResultJson);
    }

    [Fact]
    public void Execute_RetainedSalvage_DeductsSalvageValue()
    {
        var result = _executor.Execute("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 900, "salvageValueIfRetained": 4000}""");

        Assert.True(result.Success);
        Assert.Contains("14400", result.ResultJson);
    }

    [Fact]
    public void Execute_MissingRequiredArgument_Fails()
    {
        var result = _executor.Execute("""{"applicableDeductible": 500, "salesTaxAndFees": 900}""");

        Assert.False(result.Success);
        Assert.Contains("actualCashValue", result.ErrorMessage);
    }

    [Fact]
    public void Execute_OmittedOptionalSalvageValue_DefaultsToZero()
    {
        var withSalvage = _executor.Execute("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 0, "salvageValueIfRetained": 0}""");
        var withoutSalvage = _executor.Execute("""{"actualCashValue": 18000, "applicableDeductible": 500, "salesTaxAndFees": 0}""");

        Assert.Equal(withSalvage.ResultJson, withoutSalvage.ResultJson);
    }
}
