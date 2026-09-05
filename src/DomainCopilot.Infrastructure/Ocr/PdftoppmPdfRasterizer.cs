using System.Diagnostics;
using DomainCopilot.Application.Ocr;

namespace DomainCopilot.Infrastructure.Ocr;

/// <summary>
/// Rasterizes a PDF's pages to PNGs by shelling out to poppler's <c>pdftoppm</c> -- the same
/// mechanism verified live against this project's own scanned-claim corpus before this class was
/// written, not assumed to work from documentation alone.
/// </summary>
public sealed class PdftoppmPdfRasterizer(OcrOptions options) : IPdfRasterizer
{
    public async Task<IReadOnlyList<byte[]>> RasterizeToPngAsync(byte[] pdfContent, CancellationToken cancellationToken = default)
    {
        var workDir = Directory.CreateTempSubdirectory("domain-copilot-ocr-");
        try
        {
            var pdfPath = Path.Combine(workDir.FullName, "input.pdf");
            await File.WriteAllBytesAsync(pdfPath, pdfContent, cancellationToken);

            var outputPrefix = Path.Combine(workDir.FullName, "page");
            var arguments = $"-r {options.RasterizationDpi} -png \"{pdfPath}\" \"{outputPrefix}\"";
            await RunProcessAsync(options.PdftoppmBinaryPath, arguments, workDir.FullName, cancellationToken);

            var pageFiles = Directory.GetFiles(workDir.FullName, "page-*.png")
                .OrderBy(ExtractPageNumber)
                .ToList();

            var pages = new List<byte[]>(pageFiles.Count);
            foreach (var file in pageFiles)
            {
                pages.Add(await File.ReadAllBytesAsync(file, cancellationToken));
            }

            return pages;
        }
        finally
        {
            workDir.Delete(recursive: true);
        }
    }

    // pdftoppm names single-digit pages "page-1.png" but pads once there are 10+ pages
    // ("page-01.png" becomes "page-10.png" etc.) -- sorting the filename string alone would put
    // "page-10.png" before "page-2.png", so the page number is parsed out and sorted numerically.
    private static int ExtractPageNumber(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var numberPart = name[(name.LastIndexOf('-') + 1)..];
        return int.Parse(numberPart);
    }

    internal static async Task RunProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}: {stderr}");
        }
    }
}
