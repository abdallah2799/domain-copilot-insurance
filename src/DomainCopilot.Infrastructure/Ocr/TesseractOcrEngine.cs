using System.Diagnostics;
using DomainCopilot.Application.Ocr;

namespace DomainCopilot.Infrastructure.Ocr;

/// <summary>
/// OCR via Tesseract's CLI, requesting TSV output specifically so both the recognized text and
/// Tesseract's own real per-word confidence come from a single invocation -- verified live against
/// this project's own synthetic scanned-claim corpus (T6) before being wired in here, including
/// confirming what a genuinely low-confidence page's numbers actually look like, not assumed from
/// Tesseract's documentation.
/// </summary>
public sealed class TesseractOcrEngine(OcrOptions options) : IOcrEngine
{
    public async Task<(string Text, double ConfidencePercent)> RecognizeAsync(byte[] pngImage, CancellationToken cancellationToken = default)
    {
        var workDir = Directory.CreateTempSubdirectory("domain-copilot-ocr-");
        try
        {
            var imagePath = Path.Combine(workDir.FullName, "page.png");
            await File.WriteAllBytesAsync(imagePath, pngImage, cancellationToken);

            var outputPrefix = Path.Combine(workDir.FullName, "page");
            await RunTesseractAsync(imagePath, outputPrefix, cancellationToken);

            var tsvPath = $"{outputPrefix}.tsv";
            var lines = await File.ReadAllLinesAsync(tsvPath, cancellationToken);
            return ParseTsv(lines);
        }
        finally
        {
            workDir.Delete(recursive: true);
        }
    }

    private async Task RunTesseractAsync(string imagePath, string outputPrefix, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(options.TesseractBinaryPath, $"\"{imagePath}\" \"{outputPrefix}\" tsv")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        if (!string.IsNullOrEmpty(options.TesseractLibraryPath))
        {
            var existing = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
            process.StartInfo.Environment["LD_LIBRARY_PATH"] = string.IsNullOrEmpty(existing)
                ? options.TesseractLibraryPath
                : $"{options.TesseractLibraryPath}:{existing}";
        }

        if (!string.IsNullOrEmpty(options.TessDataPrefix))
        {
            process.StartInfo.Environment["TESSDATA_PREFIX"] = options.TessDataPrefix;
        }

        process.Start();
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            throw new InvalidOperationException($"tesseract exited with code {process.ExitCode}: {stderr}");
        }
    }

    // Tesseract's TSV format (one row per detected element, level 5 = word):
    // level  page_num  block_num  par_num  line_num  word_num  left  top  width  height  conf  text
    // Reconstructing text line-by-line (grouping by block/par/line) rather than joining every word
    // with a single separator preserves real paragraph structure well enough for downstream use;
    // conf is -1 on every non-word aggregate row, so only level-5 rows count toward the average.
    private static (string Text, double ConfidencePercent) ParseTsv(string[] lines)
    {
        var confidences = new List<double>();
        var currentLineKey = (Block: -1, Par: -1, Line: -1);
        var currentLineWords = new List<string>();
        var textLines = new List<string>();

        void FlushLine()
        {
            if (currentLineWords.Count > 0)
            {
                textLines.Add(string.Join(' ', currentLineWords));
                currentLineWords.Clear();
            }
        }

        foreach (var line in lines.Skip(1)) // header row
        {
            var columns = line.Split('\t');
            if (columns.Length < 12 || columns[0] != "5")
            {
                continue; // not a word-level row
            }

            // Found live against a heavily degraded test image: Tesseract can emit a level-5 row
            // spanning the entire page with empty text and a real-looking confidence value (95.0)
            // -- its way of saying "found a text region, recognized nothing in it," not a real
            // confident detection. Counting that toward the average would silently mark a page
            // that yielded zero actual text as high-confidence. Only a row with real recognized
            // text carries a meaningful confidence signal.
            if (string.IsNullOrWhiteSpace(columns[11]))
            {
                continue;
            }

            var lineKey = (Block: int.Parse(columns[2]), Par: int.Parse(columns[3]), Line: int.Parse(columns[4]));
            if (lineKey != currentLineKey)
            {
                FlushLine();
                currentLineKey = lineKey;
            }

            currentLineWords.Add(columns[11]);
            confidences.Add(double.Parse(columns[10]));
        }

        FlushLine();

        var averageConfidence = confidences.Count > 0 ? confidences.Average() : 0.0;
        return (string.Join('\n', textLines), averageConfidence);
    }
}
