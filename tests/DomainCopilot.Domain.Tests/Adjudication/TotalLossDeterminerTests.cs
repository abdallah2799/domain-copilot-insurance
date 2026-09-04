using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Domain.Tests.Adjudication;

public class TotalLossDeterminerTests
{
    [Fact]
    public void IsTotalLoss_RepairPlusSalvageExceedsAcv_ReturnsTrue()
    {
        var result = TotalLossDeterminer.IsTotalLoss(repairCost: 15000m, salvageValue: 6000m, actualCashValue: 20000m);

        Assert.True(result);
    }

    [Fact]
    public void IsTotalLoss_RepairPlusSalvageExactlyEqualsAcv_ReturnsTrue()
    {
        var result = TotalLossDeterminer.IsTotalLoss(repairCost: 14000m, salvageValue: 6000m, actualCashValue: 20000m);

        Assert.True(result);
    }

    [Fact]
    public void IsTotalLoss_RepairAloneExceeds75PercentOfAcv_ReturnsTrueEvenWithLowSalvage()
    {
        // repair 16000 > 0.75*20000=15000, but repair+salvage (16500) is still below ACV (20000) —
        // the structural-repair threshold fires independently of the repair-plus-salvage comparison.
        var result = TotalLossDeterminer.IsTotalLoss(repairCost: 16000m, salvageValue: 500m, actualCashValue: 20000m);

        Assert.True(result);
    }

    [Fact]
    public void IsTotalLoss_RepairExactly75PercentOfAcv_DoesNotExceedThreshold()
    {
        // "exceeds 75%" is strictly-greater-than; exactly 75% with low repair+salvage is not a total loss.
        var result = TotalLossDeterminer.IsTotalLoss(repairCost: 15000m, salvageValue: 0m, actualCashValue: 20000m);

        Assert.False(result);
    }

    [Fact]
    public void IsTotalLoss_NeitherConditionMet_ReturnsFalse()
    {
        var result = TotalLossDeterminer.IsTotalLoss(repairCost: 5000m, salvageValue: 1000m, actualCashValue: 20000m);

        Assert.False(result);
    }

    [Fact]
    public void IsTotalLoss_ZeroOrNegativeActualCashValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotalLossDeterminer.IsTotalLoss(repairCost: 5000m, salvageValue: 1000m, actualCashValue: 0m));
    }

    [Fact]
    public void IsTotalLoss_NegativeRepairCost_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotalLossDeterminer.IsTotalLoss(repairCost: -1m, salvageValue: 1000m, actualCashValue: 20000m));
    }

    [Fact]
    public void IsTotalLoss_NegativeSalvageValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TotalLossDeterminer.IsTotalLoss(repairCost: 5000m, salvageValue: -1m, actualCashValue: 20000m));
    }
}
