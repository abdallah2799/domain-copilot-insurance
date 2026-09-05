using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.VectorStore;

namespace DomainCopilot.Application.Tests.Retrieval;

internal sealed class FakeKeywordSearchIndex : IKeywordSearchIndex
{
    private IReadOnlyList<ScoredChunk> _results = [];

    public void SeedSearchResults(IReadOnlyList<ScoredChunk> results) => _results = results;

    public Task IndexAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        string queryText, int topK, RetrievalFilter? filter = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_results);
}
