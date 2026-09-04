using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// Metadata pre-filter applied identically by both retrieval legs (ADR-0005). A chunk matches a
/// non-null <see cref="FormVersion"/> filter when its own <c>FormVersion</c> equals it OR is null —
/// version-agnostic material (reference docs, most endorsements) must stay visible regardless of
/// which policy version governs the query, since it isn't tied to either edition.
/// </summary>
public sealed record RetrievalFilter(string? FormVersion = null, DocumentCategory? Category = null);
