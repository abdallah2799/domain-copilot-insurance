using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// One chunk as ranked by a single retrieval leg (dense or keyword) before fusion. <see cref="Score"/>
/// is that leg's own native score (cosine similarity for dense, BM25 for keyword) — not comparable
/// across legs, which is exactly why fusion (<see cref="ReciprocalRankFusion"/>) ranks by position
/// within each leg's list rather than by these raw scores directly.
/// </summary>
public sealed record ScoredChunk(
    Guid DocumentId,
    int ChunkIndex,
    string SectionTitle,
    int? PageNumber,
    DocumentCategory Category,
    string? FormVersion,
    DateOnly? EffectiveDate,
    string Text,
    double Score);
