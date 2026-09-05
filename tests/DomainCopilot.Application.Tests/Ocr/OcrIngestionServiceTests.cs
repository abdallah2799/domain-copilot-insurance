using DomainCopilot.Application.Ocr;
using DomainCopilot.Domain.Ocr;
using Microsoft.Extensions.Logging.Abstractions;

namespace DomainCopilot.Application.Tests.Ocr;

public class OcrIngestionServiceTests
{
    private static OcrIngestionService NewService(
        out FakeScannedDocumentRepository repository, out FakePdfRasterizer rasterizer, out FakeOcrEngine ocrEngine)
    {
        repository = new FakeScannedDocumentRepository();
        rasterizer = new FakePdfRasterizer();
        ocrEngine = new FakeOcrEngine();
        return new OcrIngestionService(repository, rasterizer, ocrEngine, NullLogger<OcrIngestionService>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_AllPagesAboveThreshold_ReturnsCompleted()
    {
        var service = NewService(out _, out var rasterizer, out var ocrEngine);
        rasterizer.SeedPages(1);
        ocrEngine.Enqueue("Claim intake text", 95.0);

        var result = await service.ProcessAsync(new OcrIngestionRequest("CLM-1", "intake.pdf", [1, 2, 3]));

        Assert.Equal(ScannedDocumentStatus.Completed, result.Status);
        Assert.Equal(95.0, result.OverallConfidencePercent);
    }

    [Fact]
    public async Task ProcessAsync_LowConfidencePage_ReturnsNeedsReview()
    {
        var service = NewService(out _, out var rasterizer, out var ocrEngine);
        rasterizer.SeedPages(2);
        ocrEngine.Enqueue("clear page", 96.0);
        ocrEngine.Enqueue("smudged page", 30.0);

        var result = await service.ProcessAsync(new OcrIngestionRequest("CLM-1", "intake.pdf", [1, 2, 3]));

        Assert.Equal(ScannedDocumentStatus.NeedsReview, result.Status);
        Assert.Equal(30.0, result.LowestPageConfidencePercent);
    }

    [Fact]
    public async Task ProcessAsync_RasterizationFails_ReturnsFailedRatherThanThrowing()
    {
        var service = NewService(out _, out var rasterizer, out _);
        rasterizer.SeedFailure();

        var result = await service.ProcessAsync(new OcrIngestionRequest("CLM-1", "intake.pdf", [1, 2, 3]));

        Assert.Equal(ScannedDocumentStatus.Failed, result.Status);
        Assert.Contains("simulated failure", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessAsync_SameContentHashUploadedTwice_IsIdempotent()
    {
        var service = NewService(out var repository, out var rasterizer, out var ocrEngine);
        rasterizer.SeedPages(1);
        ocrEngine.Enqueue("text", 95.0);

        var request = new OcrIngestionRequest("CLM-1", "intake.pdf", [1, 2, 3]);
        var first = await service.ProcessAsync(request);
        var second = await service.ProcessAsync(request);

        Assert.Equal(first.Id, second.Id);
        var all = await repository.ListByClaimNumberAsync("CLM-1");
        Assert.Single(all); // the second call did not create (or reprocess into) a new record
    }

    [Fact]
    public async Task ProcessAsync_DifferentClaimsSameFileBytes_AreNotTreatedAsTheSameUpload()
    {
        var service = NewService(out var repository, out var rasterizer, out var ocrEngine);
        rasterizer.SeedPages(1);
        ocrEngine.Enqueue("text", 95.0);
        ocrEngine.Enqueue("text", 95.0);

        await service.ProcessAsync(new OcrIngestionRequest("CLM-1", "intake.pdf", [1, 2, 3]));
        await service.ProcessAsync(new OcrIngestionRequest("CLM-2", "intake.pdf", [1, 2, 3]));

        Assert.Single(await repository.ListByClaimNumberAsync("CLM-1"));
        Assert.Single(await repository.ListByClaimNumberAsync("CLM-2"));
    }
}
