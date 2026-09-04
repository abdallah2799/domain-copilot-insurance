using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Domain.Tests.Adjudication;

public class StandardPayoutCalculatorTests
{
    [Fact]
    public void Calculate_DamageBelowLimit_SubtractsDeductibleFromDamage()
    {
        var payout = StandardPayoutCalculator.Calculate(estimatedDamage: 3000m, applicableLimit: 50000m, applicableDeductible: 500m);

        Assert.Equal(2500m, payout);
    }

    [Fact]
    public void Calculate_DamageAboveLimit_CapsAtLimitBeforeSubtractingDeductible()
    {
        // 50000 damage, 20000 limit, 500 deductible -> min(50000,20000) - 500 = 19500, NOT
        // min(50000-500, 20000) = 20000. The order matters and is exactly what this test locks in.
        var payout = StandardPayoutCalculator.Calculate(estimatedDamage: 50000m, applicableLimit: 20000m, applicableDeductible: 500m);

        Assert.Equal(19500m, payout);
    }

    [Fact]
    public void Calculate_DamageEqualsLimit_DeductibleStillApplied()
    {
        var payout = StandardPayoutCalculator.Calculate(estimatedDamage: 20000m, applicableLimit: 20000m, applicableDeductible: 500m);

        Assert.Equal(19500m, payout);
    }

    [Fact]
    public void Calculate_CappedDamageBelowDeductible_FlooredAtZero_NotNegative()
    {
        var payout = StandardPayoutCalculator.Calculate(estimatedDamage: 300m, applicableLimit: 50000m, applicableDeductible: 500m);

        Assert.Equal(0m, payout);
    }

    [Fact]
    public void Calculate_GlassOnlyWaiverApplies_DeductibleIgnoredEvenWhenNonZero()
    {
        var payout = StandardPayoutCalculator.Calculate(estimatedDamage: 1200m, applicableLimit: 50000m, applicableDeductible: 500m, glassOnlyDeductibleWaiverApplies: true);

        Assert.Equal(1200m, payout);
    }

    [Fact]
    public void Calculate_ZeroDamage_ReturnsZero()
    {
        var payout = StandardPayoutCalculator.Calculate(estimatedDamage: 0m, applicableLimit: 50000m, applicableDeductible: 500m);

        Assert.Equal(0m, payout);
    }

    [Fact]
    public void Calculate_ZeroDeductible_ReturnsCappedDamageUnchanged()
    {
        var payout = StandardPayoutCalculator.Calculate(estimatedDamage: 5000m, applicableLimit: 50000m, applicableDeductible: 0m);

        Assert.Equal(5000m, payout);
    }

    [Fact]
    public void Calculate_NegativeEstimatedDamage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StandardPayoutCalculator.Calculate(estimatedDamage: -1m, applicableLimit: 50000m, applicableDeductible: 500m));
    }

    [Fact]
    public void Calculate_NegativeApplicableLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StandardPayoutCalculator.Calculate(estimatedDamage: 1000m, applicableLimit: -1m, applicableDeductible: 500m));
    }

    [Fact]
    public void Calculate_NegativeApplicableDeductible_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StandardPayoutCalculator.Calculate(estimatedDamage: 1000m, applicableLimit: 50000m, applicableDeductible: -1m));
    }
}
