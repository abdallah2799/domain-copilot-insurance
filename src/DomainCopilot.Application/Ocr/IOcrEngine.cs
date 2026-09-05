namespace DomainCopilot.Application.Ocr;

/// <summary>Port over the OCR engine itself (Infrastructure: shells out to Tesseract) —
/// Application never references a specific OCR SDK/binary directly.</summary>
public interface IOcrEngine
{
    /// <summary>Runs OCR on one page image, returning both the extracted text and Tesseract's own
    /// mean per-word confidence for that page (0-100).</summary>
    Task<(string Text, double ConfidencePercent)> RecognizeAsync(byte[] pngImage, CancellationToken cancellationToken = default);
}
