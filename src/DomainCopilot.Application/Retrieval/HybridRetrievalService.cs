using DomainCopilot.Application.Documents;
using DomainCopilot.Application.Providers;
using DomainCopilot.Application.VectorStore;
using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// FR-2's retrieval orchestrator (ADR-0005): resolve the governing policy version if a date of loss
/// was given, embed the query, run the dense and keyword legs in parallel, fuse them with RRF,
/// enrich with document titles, and decide whether the evidence is strong enough to answer from.
/// </summary>
public sealed class HybridRetrievalService(
    IDocumentRepository documentRepository,
    IEmbeddingService embeddingService,
    IVectorStore vectorStore,
    IKeywordSearchIndex keywordSearchIndex)
{
    /// <summary>Minimum dense cosine-similarity score the top fused chunk must carry for the result
    /// to count as sufficient evidence — chosen empirically against this corpus's real embeddings
    /// (ADR-0005), not a default copied from elsewhere. A chunk found by keyword alone, with no
    /// dense match at all, does not meet this bar on its own; a strong keyword match without
    /// semantic similarity is treated as a weaker signal than a direct embedding match here.</summary>
    private const double MinDenseScoreForSufficientEvidence = 0.55;

    public async Task<RetrievalResult> SearchAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
    {
        string? resolvedFormVersion = query.FormVersion;
        if (resolvedFormVersion is null && query.DateOfLoss is { } dateOfLoss)
        {
            var policyForms = await documentRepository.ListByStatusAsync(IngestionStatus.Completed, cancellationToken);
            resolvedFormVersion = PolicyVersionResolver.Resolve(policyForms, dateOfLoss);
        }

        var filter = new RetrievalFilter(resolvedFormVersion, query.Category);
        var fetchCount = Math.Max(query.TopK * 3, 10);

        var queryEmbeddings = await embeddingService.EmbedAsync([query.QueryText], cancellationToken);
        var denseTask = vectorStore.SearchAsync(queryEmbeddings[0], fetchCount, filter, cancellationToken);
        var keywordTask = keywordSearchIndex.SearchAsync(query.QueryText, fetchCount, filter, cancellationToken);
        await Task.WhenAll(denseTask, keywordTask);

        var fused = ReciprocalRankFusion.Fuse(denseTask.Result, keywordTask.Result);
        var top = fused.Take(query.TopK).ToList();

        var documentIds = top.Select(f => f.Chunk.DocumentId).ToHashSet();
        var allDocuments = await documentRepository.ListAllAsync(cancellationToken);
        var documentsById = allDocuments
            .Where(d => documentIds.Contains(d.Id))
            .ToDictionary(d => d.Id);

        var cited = top
            .Where(f => documentsById.ContainsKey(f.Chunk.DocumentId))
            .Select(f =>
            {
                var document = documentsById[f.Chunk.DocumentId];
                return new CitedChunk(
                    f.Chunk.DocumentId,
                    document.Title,
                    document.SourceId,
                    f.Chunk.SectionTitle,
                    f.Chunk.Text,
                    f.Chunk.PageNumber,
                    f.Chunk.Category,
                    f.Chunk.FormVersion,
                    f.Chunk.EffectiveDate,
                    f.FusedScore,
                    f.DenseScore,
                    f.KeywordScore);
            })
            .ToList();

        var hasSufficientEvidence = cited.Count > 0
            && cited[0].DenseScore is { } topDenseScore
            && topDenseScore >= MinDenseScoreForSufficientEvidence;

        return new RetrievalResult(cited, hasSufficientEvidence, resolvedFormVersion);
    }
}
