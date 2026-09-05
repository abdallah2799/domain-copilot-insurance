namespace DomainCopilot.Domain.Adjudication;

/// <summary>
/// The one objectively-checkable anomaly indicator computed as plain arithmetic rather than left to
/// the Anomaly Analyst's judgment (Claims Adjudication Guidelines, Section 3): estimated damage
/// exceeding 60% of the vehicle's approximate market value. "Approximate" is the operative word —
/// this is a triage-level check using a preliminary value estimate, distinct from the rigorous
/// comparable-vehicle survey <see cref="TotalLossDeterminer"/> uses for the actual total-loss
/// determination later in the pipeline.
/// </summary>
public static class DamageToValueRatioChecker
{
    private const decimal ThresholdRatio = 0.60m;

    public static bool ExceedsThreshold(decimal estimatedDamage, decimal approximateVehicleValue)
    {
        if (estimatedDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDamage), "Estimated damage cannot be negative.");
        }

        if (approximateVehicleValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(approximateVehicleValue), "Approximate vehicle value must be positive.");
        }

        return estimatedDamage > ThresholdRatio * approximateVehicleValue;
    }
}
