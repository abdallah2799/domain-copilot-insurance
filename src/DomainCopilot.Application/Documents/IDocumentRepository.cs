using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Documents;

/// <summary>Port over the relational store for document ingestion tracking (FR-1). Infrastructure
/// provides the EF Core/MSSQL implementation; Application never references EF Core directly.</summary>
public interface IDocumentRepository
{
    Task<Document?> FindBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> ListByStatusAsync(IngestionStatus status, CancellationToken cancellationToken = default);

    /// <summary>FR-1 per-document status reporting — every ingested document, regardless of status.</summary>
    Task<IReadOnlyList<Document>> ListAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Document document, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
