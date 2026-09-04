using DomainCopilot.Application.VectorStore;

namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// Port over the keyword-retrieval leg of hybrid search (ADR-0005). Infrastructure persists chunk
/// text relationally and scores it with <see cref="Bm25Scorer"/> at query time — Application never
/// references the storage mechanism directly, mirroring <see cref="IVectorStore"/>'s split.
/// </summary>
public interface IKeywordSearchIndex
{
    /// <summary>Indexes (or re-indexes) every chunk of one document. Called alongside
    /// <see cref="IVectorStore.UpsertAsync"/> during ingestion so both retrieval legs stay in sync.</summary>
    Task IndexAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default);

    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        string queryText,
        int topK,
        RetrievalFilter? filter = null,
        CancellationToken cancellationToken = default);
}
