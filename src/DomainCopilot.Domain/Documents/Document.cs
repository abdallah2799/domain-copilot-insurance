namespace DomainCopilot.Domain.Documents;

/// <summary>
/// A knowledge-corpus source document tracked through ingestion (FR-1) — policy wordings,
/// exclusions, endorsement forms, and reference material that gets chunked, embedded, and
/// semantically searched. Declarations and claims are deliberately not <see cref="Document"/>s:
/// they're per-policyholder/per-claim case data, always fetched by exact key (policy/claim
/// number), never searched, and carry no business justification for living in the vector store —
/// see ADR-0004.
///
/// <see cref="SourceId"/> is the stable natural key (matches the corpus manifest's document id)
/// idempotent re-ingestion keys off of; <see cref="ContentHash"/> is what actually decides whether
/// re-ingesting the same source is a no-op or a real reprocess — an unchanged file re-ingested
/// twice must not duplicate chunks or silently reset status.
/// </summary>
public sealed class Document
{
    public Guid Id { get; private set; }
    public string SourceId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public DocumentCategory Category { get; private set; }
    public DocumentFormat Format { get; private set; }
    public string SourceFileName { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;

    /// <summary>Null for anything but PolicyForm — endorsement forms and reference material aren't
    /// versioned per policy edition.</summary>
    public string? FormVersion { get; private set; }

    /// <summary>The date this <see cref="FormVersion"/> took effect. Only meaningful alongside
    /// <see cref="FormVersion"/> — retrieval's version resolver (FR-2) reads this to pick the
    /// governing policy version for a given date of loss, per D2's named version-risk.</summary>
    public DateOnly? EffectiveDate { get; private set; }

    public IngestionStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>Set on <see cref="MarkCompleted"/> — how many chunks this document produced in
    /// Qdrant, for FR-1 status reporting without a round-trip to the vector store.</summary>
    public int ChunkCount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? IngestedAtUtc { get; private set; }

    private Document()
    {
        // EF Core materialization only — public construction goes through Create/Rehydrate.
    }

    public static Document Create(
        string sourceId,
        string title,
        DocumentCategory category,
        DocumentFormat format,
        string sourceFileName,
        string contentHash,
        string? formVersion = null,
        DateOnly? effectiveDate = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Document must have a source id.", nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("Document must have a content hash to support idempotent re-ingestion.", nameof(contentHash));
        }

        var now = DateTimeOffset.UtcNow;
        return new Document
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            Title = title,
            Category = category,
            Format = format,
            SourceFileName = sourceFileName,
            ContentHash = contentHash,
            FormVersion = formVersion,
            EffectiveDate = effectiveDate,
            Status = IngestionStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    /// <summary>True when the given content hash differs from what was last ingested — a
    /// same-hash re-ingestion is a no-op, not a reprocess, per FR-1's idempotency requirement.</summary>
    public bool NeedsReingestion(string newContentHash) => !string.Equals(ContentHash, newContentHash, StringComparison.Ordinal);

    public void BeginProcessing()
    {
        Status = IngestionStatus.Processing;
        ErrorMessage = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(int chunkCount)
    {
        if (chunkCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkCount), "A completed ingestion must have produced at least one chunk.");
        }

        Status = IngestionStatus.Completed;
        ErrorMessage = null;
        ChunkCount = chunkCount;
        var now = DateTimeOffset.UtcNow;
        UpdatedAtUtc = now;
        IngestedAtUtc = now;
    }

    public void MarkFailed(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("A failure must record why it failed.", nameof(errorMessage));
        }

        Status = IngestionStatus.Failed;
        ErrorMessage = errorMessage;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Updates content-derived fields ahead of reprocessing a changed source file, without
    /// losing the original <see cref="Id"/>/<see cref="SourceId"/> identity.</summary>
    public void UpdateContent(string title, string contentHash, string? formVersion, DateOnly? effectiveDate)
    {
        Title = title;
        ContentHash = contentHash;
        FormVersion = formVersion;
        EffectiveDate = effectiveDate;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
