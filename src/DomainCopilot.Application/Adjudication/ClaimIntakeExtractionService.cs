using DomainCopilot.Application.Ocr;

namespace DomainCopilot.Application.Adjudication;

public sealed record ClaimIntakeExtractionResult(
    string CombinedText,
    double OverallConfidencePercent,
    ExtractedClaimIntakeFields Fields);

/// <summary>
/// The other half of T6's "document in" for D2: an adjuster uploads the claim's own intake
/// paperwork instead of retyping details it already contains (claim number, policy number, date of
/// loss, police report number) -- only the narrative and loss type stay the adjuster's own input,
/// since those are judgment calls the form's raw text doesn't settle on its own.
///
/// Deliberately does not persist a <c>ScannedDocument</c> the way <see cref="OcrIngestionService"/>
/// does: that pipeline's idempotency (ADR-0010) is keyed per claim number, which is exactly the
/// field this endpoint doesn't have yet -- extracting it is the point. This is a one-shot,
/// ephemeral OCR read to seed a new run's form, not part of the audited OCR-ingestion trail; the
/// human still reviews every extracted value before a run actually starts, the same "never
/// silently trust an extraction" principle ADR-0010 already applies to OCR confidence.
/// </summary>
public sealed class ClaimIntakeExtractionService(IPdfRasterizer rasterizer, IOcrEngine ocrEngine)
{
    public async Task<ClaimIntakeExtractionResult> ExtractAsync(byte[] pdfContent, CancellationToken cancellationToken = default)
    {
        var pageImages = await rasterizer.RasterizeToPngAsync(pdfContent, cancellationToken);
        if (pageImages.Count == 0)
        {
            throw new InvalidOperationException("Rasterizing produced zero pages -- the PDF may be empty or corrupt.");
        }

        var texts = new List<string>(pageImages.Count);
        var confidences = new List<double>(pageImages.Count);
        foreach (var image in pageImages)
        {
            var (text, confidence) = await ocrEngine.RecognizeAsync(image, cancellationToken);
            texts.Add(text);
            confidences.Add(confidence);
        }

        var combinedText = string.Join("\n\n", texts);
        var overallConfidence = confidences.Count > 0 ? confidences.Average() : 0d;
        var fields = ClaimIntakeFieldExtractor.Extract(combinedText);

        return new ClaimIntakeExtractionResult(combinedText, overallConfidence, fields);
    }
}
