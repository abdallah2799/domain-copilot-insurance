namespace DomainCopilot.Domain.Documents;

/// <summary>
/// A source document tracked through ingestion (FR-1). <see cref="SourceId"/> is the stable
/// natural key (matches the corpus manifest's document id) idempotent re-ingestion keys off of;
/// <see cref="ContentHash"/> is what actually decides whether re-ingesting the same source is a
/// no-op or a real reprocess — an unchanged file re-ingested twice must not duplicate chunks or
/// silently reset status.
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

    public string? PolicyNumber { get; private set; }
    public string? FormVersion { get; private set; }
    public string? ClaimNumber { get; private set; }
    public bool RequiresOcr { get; private set; }

    public IngestionStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

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
        string? policyNumber = null,
        string? formVersion = null,
        string? claimNumber = null,
        bool requiresOcr = false)
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
            PolicyNumber = policyNumber,
            FormVersion = formVersion,
            ClaimNumber = claimNumber,
            RequiresOcr = requiresOcr,
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

    public void MarkCompleted()
    {
        Status = IngestionStatus.Completed;
        ErrorMessage = null;
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
    public void UpdateContent(string title, string contentHash, string? formVersion)
    {
        Title = title;
        ContentHash = contentHash;
        FormVersion = formVersion;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
