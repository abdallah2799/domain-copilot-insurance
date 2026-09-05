using DomainCopilot.Application.Ocr;
using DomainCopilot.Domain.Ocr;

namespace DomainCopilot.Application.Tests.Ocr;

internal sealed class FakeScannedDocumentRepository : IScannedDocumentRepository
{
    private readonly List<ScannedDocument> _documents = [];

    public Task<ScannedDocument?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.SingleOrDefault(d => d.Id == id));

    public Task<ScannedDocument?> FindByContentHashAsync(string claimNumber, string contentHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_documents.SingleOrDefault(d => d.ClaimNumber == claimNumber && d.ContentHash == contentHash));

    public Task<IReadOnlyList<ScannedDocument>> ListByClaimNumberAsync(string claimNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScannedDocument>>([.. _documents.Where(d => d.ClaimNumber == claimNumber)]);

    public Task AddAsync(ScannedDocument document, CancellationToken cancellationToken = default)
    {
        _documents.Add(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
