namespace DomainCopilot.Domain.Adjudication;

/// <summary>
/// The total-loss settlement formula from Total Loss Valuation Methodology, Section 3: ACV, less
/// the applicable deductible, plus documented sales tax and title/transfer fees, less salvage value
/// if the insured elects to retain the vehicle. Only applies once
/// <see cref="TotalLossDeterminer.IsTotalLoss"/> is true — a repairable loss uses
/// <see cref="StandardPayoutCalculator"/> instead.
/// </summary>
public static class TotalLossSettlementCalculator
{
    public static decimal Calculate(
        decimal actualCashValue,
        decimal applicableDeductible,
        decimal salesTaxAndFees,
        decimal salvageValueIfRetained = 0m)
    {
        if (actualCashValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actualCashValue), "Actual cash value cannot be negative.");
        }

        if (applicableDeductible < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(applicableDeductible), "Applicable deductible cannot be negative.");
        }

        if (salesTaxAndFees < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salesTaxAndFees), "Sales tax and fees cannot be negative.");
        }

        if (salvageValueIfRetained < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salvageValueIfRetained), "Salvage value cannot be negative.");
        }

        var settlement = actualCashValue - applicableDeductible + salesTaxAndFees - salvageValueIfRetained;

        return Math.Max(settlement, 0m);
    }
}
