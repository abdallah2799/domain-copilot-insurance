using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Retrieval;

/// <summary>One retrieval hit, carrying everything a citation needs to point back to the exact
/// source chunk (FR-2) — document identity/title, section, page, and the policy version/effective
/// date it belongs to, if any.</summary>
public sealed record CitedChunk(
    Guid DocumentId,
    string DocumentTitle,
    string DocumentSourceId,
    string SectionTitle,
    string Text,
    int? PageNumber,
    DocumentCategory Category,
    string? FormVersion,
    DateOnly? EffectiveDate,
    double FusedScore,
    double? DenseScore,
    double? KeywordScore);
