using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.VectorStore;

namespace DomainCopilot.Application.Tests.Retrieval;

internal sealed class FakeVectorStore : IVectorStore
{
    private IReadOnlyList<ScoredChunk> _results = [];

    public void SeedSearchResults(IReadOnlyList<ScoredChunk> results) => _results = results;

    public Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding, int topK, RetrievalFilter? filter = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_results);
}
