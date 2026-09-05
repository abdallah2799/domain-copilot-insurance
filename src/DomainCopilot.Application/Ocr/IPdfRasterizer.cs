namespace DomainCopilot.Application.Ocr;

/// <summary>Port over rasterizing a PDF's pages to images (Infrastructure: shells out to
/// <c>pdftoppm</c>) — Tesseract OCRs images, not PDFs directly, so a scanned PDF has to become one
/// image per page before <see cref="IOcrEngine"/> can read it.</summary>
public interface IPdfRasterizer
{
    /// <summary>Returns one PNG-encoded image per page, in page order.</summary>
    Task<IReadOnlyList<byte[]>> RasterizeToPngAsync(byte[] pdfContent, CancellationToken cancellationToken = default);
}
