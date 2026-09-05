using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Everything the T6 generated memo needs, already deserialized from
/// <see cref="AdjudicationCase"/>'s JSON-blob columns into the same typed records the agents
/// themselves produce and consume — the memo cites the same structured data the pipeline reasoned
/// over, not a re-derived summary of it.</summary>
public sealed record AdjudicationMemoData(
    AdjudicationCase Case,
    CoverageMatchResult? CoverageMatch,
    AnomalyFindings? AnomalyFindings,
    ExclusionAnalysisResult? ExclusionAnalysis,
    Recommendation? Recommendation);
