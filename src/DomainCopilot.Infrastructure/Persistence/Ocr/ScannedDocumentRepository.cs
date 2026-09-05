using DomainCopilot.Application.Ocr;
using DomainCopilot.Domain.Ocr;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.Ocr;

public sealed class ScannedDocumentRepository(DomainCopilotDbContext dbContext) : IScannedDocumentRepository
{
    public Task<ScannedDocument?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ScannedDocuments.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<ScannedDocument?> FindByContentHashAsync(string claimNumber, string contentHash, CancellationToken cancellationToken = default) =>
        dbContext.ScannedDocuments.SingleOrDefaultAsync(d => d.ClaimNumber == claimNumber && d.ContentHash == contentHash, cancellationToken);

    public async Task<IReadOnlyList<ScannedDocument>> ListByClaimNumberAsync(string claimNumber, CancellationToken cancellationToken = default) =>
        await dbContext.ScannedDocuments
            .Where(d => d.ClaimNumber == claimNumber)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(ScannedDocument document, CancellationToken cancellationToken = default)
    {
        dbContext.ScannedDocuments.Add(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
