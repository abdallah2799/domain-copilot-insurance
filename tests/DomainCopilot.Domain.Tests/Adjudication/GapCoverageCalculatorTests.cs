using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Domain.Tests.Adjudication;

public class GapCoverageCalculatorTests
{
    [Fact]
    public void Calculate_SettlementBelowBalance_GapWithinLimit_PaysFullGap()
    {
        var gap = GapCoverageCalculator.Calculate(loanOrLeaseBalance: 22000m, totalLossSettlement: 18000m, endorsementLimit: 10000m);

        Assert.Equal(4000m, gap);
    }

    [Fact]
    public void Calculate_GapExceedsEndorsementLimit_CapsAtLimit()
    {
        var gap = GapCoverageCalculator.Calculate(loanOrLeaseBalance: 30000m, totalLossSettlement: 18000m, endorsementLimit: 5000m);

        Assert.Equal(5000m, gap);
    }

    [Fact]
    public void Calculate_SettlementEqualsBalance_ReturnsZero()
    {
        var gap = GapCoverageCalculator.Calculate(loanOrLeaseBalance: 18000m, totalLossSettlement: 18000m, endorsementLimit: 10000m);

        Assert.Equal(0m, gap);
    }

    [Fact]
    public void Calculate_SettlementExceedsBalance_ReturnsZero_NotNegative()
    {
        var gap = GapCoverageCalculator.Calculate(loanOrLeaseBalance: 15000m, totalLossSettlement: 18000m, endorsementLimit: 10000m);

        Assert.Equal(0m, gap);
    }

    [Fact]
    public void Calculate_NegativeLoanOrLeaseBalance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GapCoverageCalculator.Calculate(loanOrLeaseBalance: -1m, totalLossSettlement: 18000m, endorsementLimit: 10000m));
    }

    [Fact]
    public void Calculate_NegativeTotalLossSettlement_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GapCoverageCalculator.Calculate(loanOrLeaseBalance: 22000m, totalLossSettlement: -1m, endorsementLimit: 10000m));
    }

    [Fact]
    public void Calculate_NegativeEndorsementLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GapCoverageCalculator.Calculate(loanOrLeaseBalance: 22000m, totalLossSettlement: 18000m, endorsementLimit: -1m));
    }
}
