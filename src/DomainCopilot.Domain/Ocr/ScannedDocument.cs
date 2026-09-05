using System.Text.Json;

namespace DomainCopilot.Domain.Ocr;

/// <summary>
/// A scanned claim-supporting document processed through OCR (T6) — a claim intake form, a police
/// report, or similar, as opposed to the born-digital knowledge-corpus <c>Document</c>s FR-1
/// governs. Deliberately a separate entity, not a variant of <c>Document</c>: OCR output carries a
/// confidence signal knowledge ingestion never has to reason about, and per ADR-0004, per-claim
/// paperwork is case data, never routed through the knowledge pipeline or searched.
/// </summary>
public sealed class ScannedDocument
{
    /// <summary>Below this mean per-word confidence (Tesseract's own 0-100 scale), a page is not
    /// trusted as ground truth — chosen as a first real threshold against this project's own
    /// synthetic scans (ADR-0010), not a generic OCR-industry default.</summary>
    public const double ConfidenceThresholdPercent = 80.0;

    public Guid Id { get; private set; }
    public string ClaimNumber { get; private set; } = string.Empty;
    public string SourceFileName { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public ScannedDocumentStatus Status { get; private set; }

    /// <summary>Serialized <see cref="OcrPageResult"/> list -- stored as JSON like
    /// <c>AdjudicationCase</c>'s per-stage results, deserialized by whichever caller needs the
    /// typed per-page detail rather than by the entity itself.</summary>
    public string? PageResultsJson { get; private set; }

    public string? CombinedText { get; private set; }
    public double? OverallConfidencePercent { get; private set; }
    public double? LowestPageConfidencePercent { get; private set; }
    public string? ErrorMessage { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    private ScannedDocument()
    {
        // EF Core materialization only.
    }

    public static ScannedDocument Create(string claimNumber, string sourceFileName, string contentHash)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
        {
            throw new ArgumentException("A scanned document must be associated with a claim.", nameof(claimNumber));
        }

        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("A scanned document must have a content hash to support idempotent re-processing.", nameof(contentHash));
        }

        var now = DateTimeOffset.UtcNow;
        return new ScannedDocument
        {
            Id = Guid.NewGuid(),
            ClaimNumber = claimNumber,
            SourceFileName = sourceFileName,
            ContentHash = contentHash,
            Status = ScannedDocumentStatus.Processing,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    /// <summary>Records real OCR output and decides <see cref="ScannedDocumentStatus.Completed"/>
    /// vs. <see cref="ScannedDocumentStatus.NeedsReview"/> from it — a single page below
    /// <see cref="ConfidenceThresholdPercent"/> routes the whole document to review rather than
    /// letting a confidently-wrong page hide among otherwise-good ones.</summary>
    public void RecordOcrResult(IReadOnlyList<OcrPageResult> pageResults)
    {
        if (pageResults.Count == 0)
        {
            throw new ArgumentException("OCR must produce at least one page result to record.", nameof(pageResults));
        }

        PageResultsJson = JsonSerializer.Serialize(pageResults);
        CombinedText = string.Join("\n\n", pageResults.OrderBy(p => p.PageNumber).Select(p => p.Text));
        OverallConfidencePercent = pageResults.Average(p => p.ConfidencePercent);
        LowestPageConfidencePercent = pageResults.Min(p => p.ConfidencePercent);

        Status = LowestPageConfidencePercent < ConfidenceThresholdPercent
            ? ScannedDocumentStatus.NeedsReview
            : ScannedDocumentStatus.Completed;

        var now = DateTimeOffset.UtcNow;
        UpdatedAtUtc = now;
        ProcessedAtUtc = now;
    }

    public void MarkFailed(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("A failure must record why it failed.", nameof(errorMessage));
        }

        Status = ScannedDocumentStatus.Failed;
        ErrorMessage = errorMessage;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
