namespace DomainCopilot.Domain.Adjudication;

/// <summary>
/// The Loan/Lease Gap Coverage benefit from Total Loss Valuation Methodology, Section 5: where the
/// total loss settlement is less than the amount owed on the vehicle's loan or lease, the
/// endorsement (END-GAP-01) pays the difference, subject to its own limit. Only meaningful once
/// <see cref="TotalLossSettlementCalculator"/> has produced the settlement figure this compares
/// against — it is a separate benefit computed after, not part of, that settlement.
/// </summary>
public static class GapCoverageCalculator
{
    public static decimal Calculate(decimal loanOrLeaseBalance, decimal totalLossSettlement, decimal endorsementLimit)
    {
        if (loanOrLeaseBalance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(loanOrLeaseBalance), "Loan or lease balance cannot be negative.");
        }

        if (totalLossSettlement < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalLossSettlement), "Total loss settlement cannot be negative.");
        }

        if (endorsementLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(endorsementLimit), "Endorsement limit cannot be negative.");
        }

        var gap = loanOrLeaseBalance - totalLossSettlement;

        return gap <= 0 ? 0m : Math.Min(gap, endorsementLimit);
    }
}
