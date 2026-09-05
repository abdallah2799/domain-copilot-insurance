namespace DomainCopilot.Application.Adjudication;

/// <summary>Anomaly Analyst's typed output (Claims Adjudication Guidelines, Section 3) — all five
/// corpus-listed indicators: three computed deterministically (via tools), two requiring narrative
/// judgment. Consumed by Exclusion Analyst (for the gig-economy-use signal) and Adjudication
/// Drafter (to fold flags into the final recommendation).</summary>
public sealed record AnomalyFindings(
    bool DamageToValueRatioExceeds60Percent,
    bool DuplicateClaimsWithin90Days,
    IReadOnlyList<string> DuplicateClaimNumbers,
    bool DateOfLossBeforePolicyEffectiveDate,
    bool NarrativePoliceReportMismatch,
    bool GigEconomyUseMentioned,
    bool GigEconomyEndorsementPresent,
    string Summary,
    IReadOnlyList<string> Citations);
