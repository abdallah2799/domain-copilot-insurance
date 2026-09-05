using DomainCopilot.Application.Ocr;

namespace DomainCopilot.Application.Tests.Ocr;

internal sealed class FakePdfRasterizer : IPdfRasterizer
{
    private IReadOnlyList<byte[]> _pages = [[1]];

    public void SeedPages(int count) => _pages = [.. Enumerable.Range(0, count).Select(i => new byte[] { (byte)i })];

    public void SeedFailure() => _shouldFail = true;

    private bool _shouldFail;

    public Task<IReadOnlyList<byte[]>> RasterizeToPngAsync(byte[] pdfContent, CancellationToken cancellationToken = default) =>
        _shouldFail
            ? throw new InvalidOperationException("pdftoppm exited with code 1: simulated failure")
            : Task.FromResult(_pages);
}
