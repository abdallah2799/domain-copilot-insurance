namespace DomainCopilot.Domain.Ocr;

/// <summary>
/// T6's per-page confidence requirement made a real status, not just a number attached to
/// <see cref="ScannedDocumentStatus.Completed"/>: <see cref="NeedsReview"/> is a distinct state a
/// low-confidence page (or the whole document) lands in instead of being silently trusted as
/// ground truth — the same "don't fail silently" principle <c>IngestionStatus</c> already applies
/// to knowledge-corpus ingestion (FR-1), extended to OCR's own failure mode (a bad scan, not a bad
/// file).
/// </summary>
public enum ScannedDocumentStatus
{
    Processing,
    Completed,
    NeedsReview,
    Failed,
}
