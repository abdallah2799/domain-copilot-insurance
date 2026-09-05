using DomainCopilot.Application.Documents;
using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Tests.Adjudication;

internal sealed class FakeDocumentRepository : IDocumentRepository
{
    private readonly List<Document> _documents = [];

    public void Seed(Document document) => _documents.Add(document);

    public Task<Document?> FindBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.SingleOrDefault(d => d.SourceId == sourceId));

    public Task<IReadOnlyList<Document>> ListByStatusAsync(IngestionStatus status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>([.. _documents.Where(d => d.Status == status)]);

    public Task<IReadOnlyList<Document>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Document>>([.. _documents]);

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents.Add(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
