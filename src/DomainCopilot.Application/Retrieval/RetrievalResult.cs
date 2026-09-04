namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// The outcome of a retrieval query. <see cref="HasSufficientEvidence"/> is FR-2's refusal signal:
/// false means the corpus doesn't have a strong enough match for this query, and a caller (a chat
/// endpoint, an agent) should say so rather than answering from a weak or coincidental match —
/// <see cref="Chunks"/> may still be non-empty in that case (the closest available material,
/// returned for transparency) but should not be presented as a confident answer.
/// </summary>
public sealed record RetrievalResult(
    IReadOnlyList<CitedChunk> Chunks,
    bool HasSufficientEvidence,
    string? ResolvedFormVersion);
