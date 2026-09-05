namespace DomainCopilot.Application.Adjudication;

/// <summary>Adjudication Drafter's typed output (Claims Adjudication Guidelines, Step 5) — the
/// drafted recommendation an adjuster reviews at the approval gate. <see cref="RecommendationType"/>
/// is one of "Approve", "Deny", "PartialApprove", "RequestMoreInfo" (validated by the caller, not
/// constrained to a C# enum, so a near-miss value from the model fails with a clear message rather
/// than an opaque deserialization error). <see cref="PayoutToolUsed"/> names whichever deterministic
/// tool actually produced <see cref="PayoutAmount"/> — the citation for the number itself, distinct
/// from <see cref="Citations"/>, which cite policy/exclusion text.</summary>
public sealed record Recommendation(
    string RecommendationType,
    decimal? PayoutAmount,
    string? PayoutToolUsed,
    string Summary,
    IReadOnlyList<string> Citations);
