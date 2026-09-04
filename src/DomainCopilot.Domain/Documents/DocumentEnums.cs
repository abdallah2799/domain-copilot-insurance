namespace DomainCopilot.Domain.Documents;

/// <summary>
/// Only the knowledge-corpus categories — policy text that gets searched. Declarations and claims
/// are case data (per-policyholder/per-claim facts, always fetched by exact key, never searched)
/// and are deliberately not <see cref="Document"/>s; see ADR-0004.
/// </summary>
public enum DocumentCategory
{
    PolicyForm,
    Endorsement,
    Reference
}

public enum DocumentFormat
{
    Pdf,
    Docx
}

/// <summary>
/// Per-document ingestion status (FR-1: "per-document status and failure reporting").
/// A document that fails ingestion stays queryable in this state rather than disappearing —
/// silent failure is exactly what FR-1 exists to prevent.
/// </summary>
public enum IngestionStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
