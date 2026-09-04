using DomainCopilot.Application.Documents;
using DomainCopilot.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence;

public sealed class DocumentRepository(DomainCopilotDbContext dbContext) : IDocumentRepository
{
    public Task<Document?> FindBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default) =>
        dbContext.Documents.SingleOrDefaultAsync(d => d.SourceId == sourceId, cancellationToken);

    public async Task<IReadOnlyList<Document>> ListByStatusAsync(IngestionStatus status, CancellationToken cancellationToken = default) =>
        await dbContext.Documents.Where(d => d.Status == status).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Documents.OrderBy(d => d.SourceId).ToListAsync(cancellationToken);

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        dbContext.Documents.Add(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
