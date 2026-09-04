namespace DomainCopilot.Domain.CaseData;

/// <summary>
/// One historical claim fact, used solely for the Anomaly Analyst's <c>lookup_claim_history</c>
/// tool (the 90-day duplicate-claim check) — not the knowledge corpus, not semantically searched
/// (ADR-0004), always looked up by <see cref="PolicyNumber"/> or <see cref="ClaimNumber"/>.
/// </summary>
public sealed class ClaimHistoryRecord
{
    public Guid Id { get; private set; }
    public string ClaimNumber { get; private set; } = string.Empty;
    public string PolicyNumber { get; private set; } = string.Empty;
    public DateOnly DateOfLoss { get; private set; }
    public ClaimLossType LossType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal EstimatedDamage { get; private set; }
    public string? PoliceReportNumber { get; private set; }
    public bool IsGlassOnly { get; private set; }

    /// <summary>A short reason, if any, this claim couldn't go straight through the standard
    /// payout computation and needed a human decision first — carried through from the corpus's
    /// own synthetic data, not computed here.</summary>
    public string? FlaggedAnomaly { get; private set; }

    private ClaimHistoryRecord()
    {
        // EF Core materialization only — public construction goes through Create.
    }

    public static ClaimHistoryRecord Create(
        string claimNumber,
        string policyNumber,
        DateOnly dateOfLoss,
        ClaimLossType lossType,
        string description,
        decimal estimatedDamage,
        string? policeReportNumber,
        bool isGlassOnly,
        string? flaggedAnomaly)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
        {
            throw new ArgumentException("A claim history record must have a claim number.", nameof(claimNumber));
        }

        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new ArgumentException("A claim history record must have a policy number.", nameof(policyNumber));
        }

        if (estimatedDamage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedDamage), "Estimated damage cannot be negative.");
        }

        return new ClaimHistoryRecord
        {
            Id = Guid.NewGuid(),
            ClaimNumber = claimNumber,
            PolicyNumber = policyNumber,
            DateOfLoss = dateOfLoss,
            LossType = lossType,
            Description = description,
            EstimatedDamage = estimatedDamage,
            PoliceReportNumber = policeReportNumber,
            IsGlassOnly = isGlassOnly,
            FlaggedAnomaly = flaggedAnomaly,
        };
    }
}
