namespace DomainCopilot.Application.CaseData;

/// <summary><see cref="LossType"/> is a raw string ("Collision"/"Comprehensive"/"Liability") as it
/// arrives from the source JSON — the loading service parses it to <c>ClaimLossType</c>, so a
/// malformed value fails loudly during loading rather than being silently accepted here.</summary>
public sealed record LoadClaimHistoryRequest(
    string ClaimNumber,
    string PolicyNumber,
    DateOnly DateOfLoss,
    string LossType,
    string Description,
    decimal EstimatedDamage,
    string? PoliceReportNumber,
    bool IsGlassOnly,
    string? FlaggedAnomaly);
