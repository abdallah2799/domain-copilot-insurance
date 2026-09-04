namespace DomainCopilot.Domain.Adjudication;

/// <summary>
/// Whether a covered auto is a total loss, per Total Loss Valuation Methodology, Section 1: repair
/// cost plus salvage value reaching or exceeding Actual Cash Value (ACV), OR repair cost alone
/// exceeding 75% of ACV (a structural-repair threshold, independent of the repair-plus-salvage
/// comparison). This gate decides whether <see cref="StandardPayoutCalculator"/> or
/// <see cref="TotalLossSettlementCalculator"/> governs the claim — running the wrong one produces a
/// materially wrong payout even with correct inputs.
/// </summary>
public static class TotalLossDeterminer
{
    private const decimal RepairCostThresholdRatio = 0.75m;

    public static bool IsTotalLoss(decimal repairCost, decimal salvageValue, decimal actualCashValue)
    {
        if (repairCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(repairCost), "Repair cost cannot be negative.");
        }

        if (salvageValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salvageValue), "Salvage value cannot be negative.");
        }

        if (actualCashValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actualCashValue), "Actual cash value must be positive to evaluate total loss.");
        }

        return repairCost + salvageValue >= actualCashValue || repairCost > RepairCostThresholdRatio * actualCashValue;
    }
}
