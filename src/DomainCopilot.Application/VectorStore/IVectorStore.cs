namespace DomainCopilot.Application.VectorStore;

/// <summary>Port over the vector store (Qdrant in Infrastructure — ADR-0002). Application never
/// references the Qdrant SDK directly.</summary>
public interface IVectorStore
{
    Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default);

    Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default);

    /// <summary>Deletes every chunk previously indexed for a document — called before re-indexing a
    /// changed document, so a shrinking document doesn't leave stale trailing chunks behind.</summary>
    Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
}
