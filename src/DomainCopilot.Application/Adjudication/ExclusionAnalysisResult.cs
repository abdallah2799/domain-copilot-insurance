namespace DomainCopilot.Application.Adjudication;

/// <summary>Exclusion Analyst's typed output (Claims Adjudication Guidelines, Step 3) — given
/// Coverage Matcher's and Anomaly Analyst's outputs as input, purely decides which exclusions
/// apply. Per the corpus's own rule: if the narrative doesn't contain enough information to confirm
/// or rule out an exclusion, <see cref="InsufficientInformation"/> is true rather than assuming
/// either way.</summary>
public sealed record ExclusionAnalysisResult(
    bool ExclusionsApply,
    IReadOnlyList<string> ApplicableExclusions,
    bool InsufficientInformation,
    string Reasoning,
    IReadOnlyList<string> Citations);
