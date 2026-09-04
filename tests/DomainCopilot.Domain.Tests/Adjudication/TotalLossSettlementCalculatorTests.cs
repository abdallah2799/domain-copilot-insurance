using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Domain.Tests.Adjudication;

public class TotalLossSettlementCalculatorTests
{
    [Fact]
    public void Calculate_SurrenderScenario_NoSalvageDeduction()
    {
        var settlement = TotalLossSettlementCalculator.Calculate(actualCashValue: 18000m, applicableDeductible: 500m, salesTaxAndFees: 900m);

        Assert.Equal(18400m, settlement);
    }

    [Fact]
    public void Calculate_OwnerRetainsSalvage_SalvageValueDeducted()
    {
        var settlement = TotalLossSettlementCalculator.Calculate(
            actualCashValue: 18000m, applicableDeductible: 500m, salesTaxAndFees: 900m, salvageValueIfRetained: 4000m);

        Assert.Equal(14400m, settlement);
    }

    [Fact]
    public void Calculate_NoSalesTaxOrFees_DefaultsCorrectly()
    {
        var settlement = TotalLossSettlementCalculator.Calculate(actualCashValue: 18000m, applicableDeductible: 500m, salesTaxAndFees: 0m);

        Assert.Equal(17500m, settlement);
    }

    [Fact]
    public void Calculate_ResultWouldBeNegative_FlooredAtZero()
    {
        var settlement = TotalLossSettlementCalculator.Calculate(
            actualCashValue: 1000m, applicableDeductible: 500m, salesTaxAndFees: 0m, salvageValueIfRetained: 900m);

        Assert.Equal(0m, settlement);
    }

    [Fact]
    public void Calculate_NegativeActualCashValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotalLossSettlementCalculator.Calculate(actualCashValue: -1m, applicableDeductible: 500m, salesTaxAndFees: 0m));
    }

    [Fact]
    public void Calculate_NegativeDeductible_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotalLossSettlementCalculator.Calculate(actualCashValue: 18000m, applicableDeductible: -1m, salesTaxAndFees: 0m));
    }

    [Fact]
    public void Calculate_NegativeSalesTaxAndFees_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotalLossSettlementCalculator.Calculate(actualCashValue: 18000m, applicableDeductible: 500m, salesTaxAndFees: -1m));
    }

    [Fact]
    public void Calculate_NegativeSalvageValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotalLossSettlementCalculator.Calculate(actualCashValue: 18000m, applicableDeductible: 500m, salesTaxAndFees: 0m, salvageValueIfRetained: -1m));
    }
}
