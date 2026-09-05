namespace DomainCopilot.Application.Adjudication;

/// <summary>Coverage Matcher's typed output (Claims Adjudication Guidelines, Steps 1-2) — the
/// governing policy version, whether the invoked coverage part is actually held, and its limit/
/// deductible. Consumed as input by every later stage; never re-derived by them.</summary>
public sealed record CoverageMatchResult(
    string FormVersion,
    DateOnly FormVersionEffectiveDate,
    string CoveragePart,
    bool CoveragePartSelected,
    decimal? ApplicableLimit,
    decimal? ApplicableDeductible,
    bool GlassOnlyDeductibleWaiverApplies,
    IReadOnlyList<string> EndorsementsHeld,
    IReadOnlyList<string> Citations,
    string? Notes);
