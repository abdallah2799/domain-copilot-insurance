namespace DomainCopilot.Domain.Adjudication;

/// <summary>
/// The state of one adjudication run, mirroring the D2 workflow exactly (match version/coverage →
/// detect anomalies → analyze exclusions → draft → adjuster approval — see the multi-agent design
/// review), so the status field alone shows which stage is active, satisfying FR-5's "every run
/// inspectable step-by-step" without needing a separate stage-tracking field.
/// </summary>
public enum AdjudicationRunStatus
{
    Pending,
    MatchingCoverage,
    DetectingAnomalies,
    AnalyzingExclusions,
    Drafting,
    AwaitingApproval,
    Approved,
    Rejected,
    EditedAndApproved,
    Failed,
}
