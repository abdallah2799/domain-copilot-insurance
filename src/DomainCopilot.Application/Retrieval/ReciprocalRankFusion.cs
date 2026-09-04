namespace DomainCopilot.Application.Retrieval;

/// <summary>One chunk after fusing the dense and keyword legs. <see cref="DenseScore"/>/<see cref="KeywordScore"/>
/// are each leg's own native score where the chunk appeared in that leg's results, null otherwise —
/// a chunk found by only one leg still ranks, just with the other score absent, not zero.</summary>
public sealed record FusedChunk(ScoredChunk Chunk, double FusedScore, double? DenseScore, double? KeywordScore);

/// <summary>
/// Reciprocal Rank Fusion (ADR-0005): combines two independently-ranked chunk lists (dense cosine
/// similarity, keyword BM25) by each chunk's *rank position* within each list, not by its raw score
/// — the two legs' scores aren't on comparable scales (cosine similarity in [-1,1] vs. an
/// unbounded BM25 sum), so merging by rank is the standard way to fuse rankings from
/// incommensurable scoring functions without the fusion being dominated by whichever leg happens to
/// produce larger numbers.
/// </summary>
public static class ReciprocalRankFusion
{
    /// <summary>The rank-damping constant from the original RRF paper (Cormack et al., 2009).
    /// Larger k flattens the influence of rank differences further down each list; 60 is the
    /// paper's own default and is not tuned per-corpus here.</summary>
    private const int K = 60;

    public static IReadOnlyList<FusedChunk> Fuse(
        IReadOnlyList<ScoredChunk> denseRanked,
        IReadOnlyList<ScoredChunk> keywordRanked)
    {
        var byKey = new Dictionary<(Guid DocumentId, int ChunkIndex), (ScoredChunk Chunk, double Fused, double? Dense, double? Keyword)>();

        for (var rank = 0; rank < denseRanked.Count; rank++)
        {
            var chunk = denseRanked[rank];
            var key = (chunk.DocumentId, chunk.ChunkIndex);
            var contribution = 1.0 / (K + rank + 1);
            byKey[key] = (chunk, contribution, chunk.Score, null);
        }

        for (var rank = 0; rank < keywordRanked.Count; rank++)
        {
            var chunk = keywordRanked[rank];
            var key = (chunk.DocumentId, chunk.ChunkIndex);
            var contribution = 1.0 / (K + rank + 1);

            if (byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = (existing.Chunk, existing.Fused + contribution, existing.Dense, chunk.Score);
            }
            else
            {
                byKey[key] = (chunk, contribution, null, chunk.Score);
            }
        }

        return [.. byKey.Values
            .OrderByDescending(v => v.Fused)
            .Select(v => new FusedChunk(v.Chunk, v.Fused, v.Dense, v.Keyword))];
    }
}
