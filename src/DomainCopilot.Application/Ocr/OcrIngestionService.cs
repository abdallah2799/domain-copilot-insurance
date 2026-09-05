using System.Security.Cryptography;
using DomainCopilot.Domain.Ocr;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Ocr;

/// <summary>
/// T6's OCR pipeline: rasterize each page (<see cref="IPdfRasterizer"/>), OCR it
/// (<see cref="IOcrEngine"/>), and let <see cref="ScannedDocument.RecordOcrResult"/> decide
/// completed vs. needs-review from the real per-page confidence — never silently trusting a
/// low-confidence page as ground truth. Idempotent on content hash per claim, same principle as
/// <c>KnowledgeIngestionService</c> (FR-1), so re-uploading an unchanged scan doesn't reprocess it.
/// </summary>
public sealed class OcrIngestionService(
    IScannedDocumentRepository repository,
    IPdfRasterizer rasterizer,
    IOcrEngine ocrEngine,
    ILogger<OcrIngestionService> logger)
{
    public async Task<ScannedDocument> ProcessAsync(OcrIngestionRequest request, CancellationToken cancellationToken = default)
    {
        var contentHash = ComputeHash(request.PdfContent);
        var existing = await repository.FindByContentHashAsync(request.ClaimNumber, contentHash, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var document = ScannedDocument.Create(request.ClaimNumber, request.SourceFileName, contentHash);
        await repository.AddAsync(document, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        try
        {
            var pageImages = await rasterizer.RasterizeToPngAsync(request.PdfContent, cancellationToken);
            if (pageImages.Count == 0)
            {
                throw new InvalidOperationException("Rasterizing produced zero pages -- the PDF may be empty or corrupt.");
            }

            var pageResults = new List<OcrPageResult>(pageImages.Count);
            for (var i = 0; i < pageImages.Count; i++)
            {
                var (text, confidence) = await ocrEngine.RecognizeAsync(pageImages[i], cancellationToken);
                pageResults.Add(new OcrPageResult(i + 1, text, confidence));
            }

            document.RecordOcrResult(pageResults);
            if (document.Status == ScannedDocumentStatus.NeedsReview)
            {
                logger.LogWarning(
                    "Scanned document {DocumentId} for claim {ClaimNumber} needs human review -- lowest page confidence {Confidence:F1} is below the {Threshold:F0} threshold.",
                    document.Id, request.ClaimNumber, document.LowestPageConfidencePercent, ScannedDocument.ConfidenceThresholdPercent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OCR failed for claim {ClaimNumber}, file {FileName}.", request.ClaimNumber, request.SourceFileName);
            document.MarkFailed(ex.Message);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return document;
    }

    private static string ComputeHash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
}
