namespace DomainCopilot.Domain.Ocr;

/// <summary>One page's OCR output and confidence (T6). <see cref="ConfidencePercent"/> is the mean
/// per-word confidence Tesseract reports for that page (0-100) — the same signal
/// <see cref="ScannedDocument.LowestPageConfidencePercent"/> and the completed/needs-review
/// decision are both computed from.</summary>
public sealed record OcrPageResult(int PageNumber, string Text, double ConfidencePercent);
