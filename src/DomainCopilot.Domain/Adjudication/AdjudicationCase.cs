namespace DomainCopilot.Domain.Adjudication;

/// <summary>
/// One claim's adjudication run — the aggregate the multi-agent workflow (FR-4/FR-5) operates
/// against. <see cref="Id"/> is the "run ID" FR-5 requires every run be inspectable by. Each stage's
/// agent output is stored as its own JSON blob (the four stages' typed shapes have almost no field
/// overlap, so a normalized relational schema across them would mean mostly-null columns — the same
/// reasoning ADR-0004 already applied to the knowledge-vs-case-data split) and recording one
/// advances <see cref="Status"/> to the next stage in the same call, so the pipeline's own state
/// machine is what enforces "no recommendation reaches the adjuster without passing through every
/// prior stage" (OBJ-3) — not prompt discipline, which can be wrong with confidence.
/// </summary>
public sealed class AdjudicationCase
{
    public Guid Id { get; private set; }
    public string ClaimNumber { get; private set; } = string.Empty;
    public string PolicyNumber { get; private set; } = string.Empty;
    public DateOnly DateOfLoss { get; private set; }
    public AdjudicationRunStatus Status { get; private set; }

    public string? CoverageMatchResultJson { get; private set; }
    public string? AnomalyFindingsJson { get; private set; }
    public string? ExclusionAnalysisResultJson { get; private set; }
    public string? RecommendationJson { get; private set; }

    public string? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string? AdjusterComments { get; private set; }
    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private AdjudicationCase()
    {
        // EF Core materialization only — public construction goes through Create.
    }

    public static AdjudicationCase Create(string claimNumber, string policyNumber, DateOnly dateOfLoss)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
        {
            throw new ArgumentException("An adjudication case must have a claim number.", nameof(claimNumber));
        }

        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new ArgumentException("An adjudication case must have a policy number.", nameof(policyNumber));
        }

        var now = DateTimeOffset.UtcNow;
        return new AdjudicationCase
        {
            Id = Guid.NewGuid(),
            ClaimNumber = claimNumber,
            PolicyNumber = policyNumber,
            DateOfLoss = dateOfLoss,
            Status = AdjudicationRunStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void BeginCoverageMatching() => Transition(AdjudicationRunStatus.Pending, AdjudicationRunStatus.MatchingCoverage);

    public void RecordCoverageMatch(string coverageMatchResultJson)
    {
        RequireNonEmpty(coverageMatchResultJson, nameof(coverageMatchResultJson));
        CoverageMatchResultJson = coverageMatchResultJson;
        Transition(AdjudicationRunStatus.MatchingCoverage, AdjudicationRunStatus.DetectingAnomalies);
    }

    public void RecordAnomalyFindings(string anomalyFindingsJson)
    {
        RequireNonEmpty(anomalyFindingsJson, nameof(anomalyFindingsJson));
        AnomalyFindingsJson = anomalyFindingsJson;
        Transition(AdjudicationRunStatus.DetectingAnomalies, AdjudicationRunStatus.AnalyzingExclusions);
    }

    public void RecordExclusionAnalysis(string exclusionAnalysisResultJson)
    {
        RequireNonEmpty(exclusionAnalysisResultJson, nameof(exclusionAnalysisResultJson));
        ExclusionAnalysisResultJson = exclusionAnalysisResultJson;
        Transition(AdjudicationRunStatus.AnalyzingExclusions, AdjudicationRunStatus.Drafting);
    }

    /// <summary>Records the Adjudication Drafter's proposed recommendation and halts the run at the
    /// human gate — Claims Adjudication Guidelines, Step 5: no recommendation is final without
    /// explicit adjuster approval, so this is the last state a fully-automated call reaches.</summary>
    public void RecordRecommendation(string recommendationJson)
    {
        RequireNonEmpty(recommendationJson, nameof(recommendationJson));
        RecommendationJson = recommendationJson;
        Transition(AdjudicationRunStatus.Drafting, AdjudicationRunStatus.AwaitingApproval);
    }

    public void Approve(string approvedBy)
    {
        RequireNonEmpty(approvedBy, nameof(approvedBy));
        RecordApproval(AdjudicationRunStatus.Approved, approvedBy, comments: null);
    }

    public void Reject(string rejectedBy, string reason)
    {
        RequireNonEmpty(rejectedBy, nameof(rejectedBy));
        RequireNonEmpty(reason, nameof(reason));
        RecordApproval(AdjudicationRunStatus.Rejected, rejectedBy, reason);
    }

    /// <summary>The adjuster's edited figures/citations replace the drafted recommendation — the
    /// original AI-drafted version is not retained once edited, consistent with this system's rule
    /// that only the human-approved figure is ever the operative one.</summary>
    public void EditAndApprove(string approvedBy, string editedRecommendationJson, string editComments)
    {
        RequireNonEmpty(approvedBy, nameof(approvedBy));
        RequireNonEmpty(editedRecommendationJson, nameof(editedRecommendationJson));
        RequireNonEmpty(editComments, nameof(editComments));
        RecommendationJson = editedRecommendationJson;
        RecordApproval(AdjudicationRunStatus.EditedAndApproved, approvedBy, editComments);
    }

    /// <summary>The graceful-degrade path (FR-5): callable from any non-terminal status when a step
    /// can't complete after retries, so a run always ends in a state that shows what happened rather
    /// than being left silently stuck.</summary>
    public void MarkFailed(string reason)
    {
        RequireNonEmpty(reason, nameof(reason));

        if (IsTerminal(Status))
        {
            throw new InvalidOperationException($"Cannot fail an adjudication case already in a terminal status ({Status}).");
        }

        FailureReason = reason;
        Status = AdjudicationRunStatus.Failed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private void RecordApproval(AdjudicationRunStatus targetStatus, string approvedBy, string? comments)
    {
        if (Status != AdjudicationRunStatus.AwaitingApproval)
        {
            throw new InvalidOperationException(
                $"Cannot record an adjuster decision while the case is in status {Status} — it must be AwaitingApproval.");
        }

        ApprovedBy = approvedBy;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        AdjusterComments = comments;
        Status = targetStatus;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private void Transition(AdjudicationRunStatus expectedCurrent, AdjudicationRunStatus next)
    {
        if (Status != expectedCurrent)
        {
            throw new InvalidOperationException(
                $"Cannot move to {next} from status {Status} — expected {expectedCurrent}. The pipeline stages must run in order.");
        }

        Status = next;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static bool IsTerminal(AdjudicationRunStatus status) => status
        is AdjudicationRunStatus.Approved
        or AdjudicationRunStatus.Rejected
        or AdjudicationRunStatus.EditedAndApproved
        or AdjudicationRunStatus.Failed;

    private static void RequireNonEmpty(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", paramName);
        }
    }
}
