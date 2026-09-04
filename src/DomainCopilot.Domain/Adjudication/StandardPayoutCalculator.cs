namespace DomainCopilot.Domain.Adjudication;

/// <summary>
/// The standard repairable-loss payout formula from the Claims Adjudication Guidelines, Step 4:
/// <c>payout = min(estimated_damage, applicable_limit) - applicable_deductible</c>, floored at
/// zero. This is D2's central non-negotiable control — every payout figure this system produces
/// traces to this exact, unit-tested arithmetic, never to an LLM's narrative judgment. Does not
/// apply once a vehicle is a total loss (<see cref="TotalLossDeterminer"/>) — see Total Loss
/// Valuation Methodology, Section 4.
/// </summary>
public static class StandardPayoutCalculator
{
    public static decimal Calculate(
        decimal estimatedDamage,
        decimal applicableLimit,
        decimal applicableDeductible,
        bool glassOnlyDeductibleWaiverApplies = false)
    {
        if (estimatedDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDamage), "Estimated damage cannot be negative.");
        }

        if (applicableLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(applicableLimit), "Applicable limit cannot be negative.");
        }

        if (applicableDeductible < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(applicableDeductible), "Applicable deductible cannot be negative.");
        }

        // The limit caps estimated damage first, and the deductible is subtracted from that capped
        // figure — not the reverse. Reversing the order changes the result whenever damage exceeds
        // the limit (Deductible Selection and Application Reference, Section 2).
        var deductible = glassOnlyDeductibleWaiverApplies ? 0m : applicableDeductible;
        var cappedDamage = Math.Min(estimatedDamage, applicableLimit);
        var payout = cappedDamage - deductible;

        return Math.Max(payout, 0m);
    }
}
