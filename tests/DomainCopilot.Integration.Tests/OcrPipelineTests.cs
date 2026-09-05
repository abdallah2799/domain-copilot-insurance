using DomainCopilot.Infrastructure.Ocr;

namespace DomainCopilot.Integration.Tests;

/// <summary>
/// Real OCR against a real corpus file (T6) -- not a fake, unlike the orchestration-logic tests in
/// DomainCopilot.Application.Tests. This is the same live path verified by hand before any of the
/// OCR code was written: rasterize a real scanned claim intake PDF with pdftoppm, OCR it with
/// Tesseract, and check the actual confidence numbers a real, clean synthetic scan produces.
///
/// Reads Ocr__* the same way appsettings/.env would, purely via environment variables, so this
/// runs correctly both in CI (a normal `apt install tesseract-ocr poppler-utils`, no overrides
/// needed) and on this project's own dev machine (no root -- see .env's Ocr__TesseractBinaryPath
/// etc. pointing at a user-local extracted install).
/// </summary>
public class OcrPipelineTests
{
    private static OcrOptions OptionsFromEnvironment() => new()
    {
        TesseractBinaryPath = Environment.GetEnvironmentVariable("Ocr__TesseractBinaryPath") ?? "tesseract",
        TesseractLibraryPath = Environment.GetEnvironmentVariable("Ocr__TesseractLibraryPath"),
        TessDataPrefix = Environment.GetEnvironmentVariable("Ocr__TessDataPrefix"),
        PdftoppmBinaryPath = Environment.GetEnvironmentVariable("Ocr__PdftoppmBinaryPath") ?? "pdftoppm",
    };

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "seed-data")))
        {
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        return dir ?? throw new InvalidOperationException("Could not locate the repo root (no ancestor directory contains seed-data/).");
    }

    [Fact]
    public async Task RealScannedClaimIntakeForm_ProducesHighConfidenceCompletedResult()
    {
        var samplePath = Path.Combine(FindRepoRoot(), "seed-data", "corpus", "claims", "intake_clm_2025_04417.pdf");
        var pdfContent = await File.ReadAllBytesAsync(samplePath);

        var rasterizer = new PdftoppmPdfRasterizer(OptionsFromEnvironment());
        var ocrEngine = new TesseractOcrEngine(OptionsFromEnvironment());

        var pageImages = await rasterizer.RasterizeToPngAsync(pdfContent);
        Assert.NotEmpty(pageImages);

        var (text, confidence) = await ocrEngine.RecognizeAsync(pageImages[0]);

        // Real assertions against real OCR output, not a placeholder -- this is a clean synthetic
        // scan (ADR-0010's generator adds only mild grain/rotation/blur), so a real, well-behaved
        // OCR engine should read it well above the review threshold and get the claim number right.
        Assert.True(confidence > DomainCopilot.Domain.Ocr.ScannedDocument.ConfidenceThresholdPercent,
            $"Expected a clean synthetic scan to OCR above the {DomainCopilot.Domain.Ocr.ScannedDocument.ConfidenceThresholdPercent} threshold, got {confidence:F1}.");
        Assert.Contains("CLM-2025-04417", text);
        Assert.Contains("Meridian Mutual", text);
    }

    [Fact]
    public async Task DegradedScan_ProducesLowerConfidenceThanTheCleanOriginal()
    {
        var samplePath = Path.Combine(FindRepoRoot(), "seed-data", "corpus", "claims", "intake_clm_2025_04417.pdf");
        var pdfContent = await File.ReadAllBytesAsync(samplePath);
        var ocrEngine = new TesseractOcrEngine(OptionsFromEnvironment());

        var cleanRasterizer = new PdftoppmPdfRasterizer(OptionsFromEnvironment());
        var cleanPages = await cleanRasterizer.RasterizeToPngAsync(pdfContent);
        var (_, cleanConfidence) = await ocrEngine.RecognizeAsync(cleanPages[0]);

        // A genuinely bad scan, simulated the simplest real way available without adding an image
        // library dependency just for this one test: the same PDF rasterized at a far lower DPI, so
        // the text is actually blurrier and lower-detail -- a real, common bad-scan cause (a low
        // resolution scanner setting), not a synthetic pixel-corruption stand-in for one.
        var degradedOptions = OptionsFromEnvironment();
        degradedOptions.RasterizationDpi = 25;
        var degradedRasterizer = new PdftoppmPdfRasterizer(degradedOptions);
        var degradedPages = await degradedRasterizer.RasterizeToPngAsync(pdfContent);
        var (_, degradedConfidence) = await ocrEngine.RecognizeAsync(degradedPages[0]);

        // Not asserting the degraded confidence lands below the review threshold specifically --
        // that depends on exactly how much a real OCR engine's confidence drops for a given amount
        // of real degradation, which isn't something to hardcode an exact number for. What this
        // does prove, for real: heavier degradation measurably hurts a real OCR engine's own
        // confidence, which is the actual signal ScannedDocument.RecordOcrResult's review routing
        // depends on -- see OcrIngestionServiceTests for the routing decision itself, exercised
        // against a fake engine with a controlled confidence value.
        Assert.True(degradedConfidence < cleanConfidence,
            $"Expected a much-lower-DPI rasterization to reduce OCR confidence (clean: {cleanConfidence:F1}, degraded: {degradedConfidence:F1}).");
    }
}
