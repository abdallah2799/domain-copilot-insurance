namespace DomainCopilot.Infrastructure.Ocr;

/// <summary>
/// Binary/library locations for the two external processes T6's OCR pipeline shells out to
/// (Tesseract, poppler's <c>pdftoppm</c>). Defaults assume a normal system install (both binaries
/// resolved via PATH, Tesseract finding its own bundled tessdata) -- <see cref="TesseractLibraryPath"/>
/// and <see cref="TessDataPrefix"/> only need setting when Tesseract was installed somewhere
/// non-standard (this project's own dev machine extracted its .deb packages into a user-local
/// prefix rather than a system-wide install, since it has no root access -- see .env).
/// </summary>
public sealed class OcrOptions
{
    public const string SectionName = "Ocr";

    public string TesseractBinaryPath { get; set; } = "tesseract";
    public string? TesseractLibraryPath { get; set; }
    public string? TessDataPrefix { get; set; }

    public string PdftoppmBinaryPath { get; set; } = "pdftoppm";

    /// <summary>200dpi verified live against this project's own synthetic scanned corpus (T6) as
    /// enough resolution for Tesseract to read cleanly without unnecessarily large intermediate
    /// images.</summary>
    public int RasterizationDpi { get; set; } = 200;
}
