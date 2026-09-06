using DomainCopilot.Application.Adjudication;
using DomainCopilot.Application.Tests.Ocr;

namespace DomainCopilot.Application.Tests.Adjudication;

public class ClaimIntakeExtractionServiceTests
{
    [Fact]
    public async Task ExtractAsync_CombinesPagesAndAveragesConfidence_ThenExtractsFields()
    {
        var rasterizer = new FakePdfRasterizer();
        rasterizer.SeedPages(2);
        var ocrEngine = new FakeOcrEngine();
        ocrEngine.Enqueue("Claim Number: CLM-2025-04417\nPolicy Number: MMIC-PAP-100234", 96.0);
        ocrEngine.Enqueue("Date of Loss: 2025-08-03\nPolice Report Number: CPD-2025-231044", 88.0);
        var sut = new ClaimIntakeExtractionService(rasterizer, ocrEngine);

        var result = await sut.ExtractAsync([1, 2, 3]);

        Assert.Equal(92.0, result.OverallConfidencePercent);
        Assert.Contains("CLM-2025-04417", result.CombinedText);
        Assert.Equal("CLM-2025-04417", result.Fields.ClaimNumber);
        Assert.Equal("MMIC-PAP-100234", result.Fields.PolicyNumber);
        Assert.Equal(new DateOnly(2025, 8, 3), result.Fields.DateOfLoss);
        Assert.Equal("CPD-2025-231044", result.Fields.PoliceReportNumber);
    }

    [Fact]
    public async Task ExtractAsync_ZeroPagesRasterized_Throws()
    {
        var rasterizer = new FakePdfRasterizer();
        rasterizer.SeedPages(0);
        var sut = new ClaimIntakeExtractionService(rasterizer, new FakeOcrEngine());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExtractAsync([1, 2, 3]));
    }

    [Fact]
    public async Task ExtractAsync_NoLabelsFound_ReturnsNullFieldsRatherThanThrowing()
    {
        var rasterizer = new FakePdfRasterizer();
        rasterizer.SeedPages(1);
        var ocrEngine = new FakeOcrEngine();
        ocrEngine.Enqueue("An illegible or unrelated scan.", 40.0);
        var sut = new ClaimIntakeExtractionService(rasterizer, ocrEngine);

        var result = await sut.ExtractAsync([1]);

        Assert.Null(result.Fields.ClaimNumber);
        Assert.Null(result.Fields.PolicyNumber);
    }
}
