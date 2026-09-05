using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Domain.Tests.Adjudication;

public class DamageToValueRatioCheckerTests
{
    [Fact]
    public void ExceedsThreshold_DamageAbove60Percent_ReturnsTrue()
    {
        Assert.True(DamageToValueRatioChecker.ExceedsThreshold(estimatedDamage: 13000m, approximateVehicleValue: 20000m));
    }

    [Fact]
    public void ExceedsThreshold_DamageExactly60Percent_DoesNotExceed()
    {
        // "exceeds 60%" is strictly-greater-than, matching the same convention already used for the
        // 75% total-loss threshold (TotalLossDeterminerTests).
        Assert.False(DamageToValueRatioChecker.ExceedsThreshold(estimatedDamage: 12000m, approximateVehicleValue: 20000m));
    }

    [Fact]
    public void ExceedsThreshold_DamageBelowThreshold_ReturnsFalse()
    {
        Assert.False(DamageToValueRatioChecker.ExceedsThreshold(estimatedDamage: 3000m, approximateVehicleValue: 20000m));
    }

    [Fact]
    public void ExceedsThreshold_ZeroVehicleValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DamageToValueRatioChecker.ExceedsThreshold(3000m, 0m));
    }

    [Fact]
    public void ExceedsThreshold_NegativeDamage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DamageToValueRatioChecker.ExceedsThreshold(-1m, 20000m));
    }
}
