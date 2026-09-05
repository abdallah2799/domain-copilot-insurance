using DomainCopilot.Domain.Ocr;

namespace DomainCopilot.Application.Ocr;

/// <summary>Port over the relational store for scanned/OCR'd claim documents (T6). Infrastructure
/// provides the EF Core/MSSQL implementation; Application never references EF Core directly.</summary>
public interface IScannedDocumentRepository
{
    Task<ScannedDocument?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Idempotency key for re-uploads: an unchanged file re-submitted for the same claim
    /// returns the existing record rather than reprocessing it.</summary>
    Task<ScannedDocument?> FindByContentHashAsync(string claimNumber, string contentHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScannedDocument>> ListByClaimNumberAsync(string claimNumber, CancellationToken cancellationToken = default);

    Task AddAsync(ScannedDocument document, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
