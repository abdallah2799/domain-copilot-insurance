using DomainCopilot.Domain.Ocr;

namespace DomainCopilot.Domain.Tests.Ocr;

public class ScannedDocumentTests
{
    [Fact]
    public void Create_MissingClaimNumber_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScannedDocument.Create("", "file.pdf", "hash"));
    }

    [Fact]
    public void Create_MissingContentHash_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScannedDocument.Create("CLM-1", "file.pdf", ""));
    }

    [Fact]
    public void RecordOcrResult_AllPagesAboveThreshold_CompletesWithAggregatedConfidence()
    {
        var document = ScannedDocument.Create("CLM-1", "intake.pdf", "hash");

        document.RecordOcrResult([
            new OcrPageResult(1, "page one text", 95.0),
            new OcrPageResult(2, "page two text", 90.0),
        ]);

        Assert.Equal(ScannedDocumentStatus.Completed, document.Status);
        Assert.Equal(92.5, document.OverallConfidencePercent);
        Assert.Equal(90.0, document.LowestPageConfidencePercent);
        Assert.Equal("page one text\n\npage two text", document.CombinedText);
        Assert.NotNull(document.ProcessedAtUtc);
    }

    [Fact]
    public void RecordOcrResult_OnePageBelowThreshold_RoutesWholeDocumentToNeedsReview()
    {
        var document = ScannedDocument.Create("CLM-1", "intake.pdf", "hash");

        document.RecordOcrResult([
            new OcrPageResult(1, "clear page", 95.0),
            new OcrPageResult(2, "blurry page", 45.0),
        ]);

        Assert.Equal(ScannedDocumentStatus.NeedsReview, document.Status);
        Assert.Equal(45.0, document.LowestPageConfidencePercent);
    }

    [Fact]
    public void RecordOcrResult_ConfidenceExactlyAtThreshold_Completes()
    {
        var document = ScannedDocument.Create("CLM-1", "intake.pdf", "hash");

        document.RecordOcrResult([new OcrPageResult(1, "text", ScannedDocument.ConfidenceThresholdPercent)]);

        Assert.Equal(ScannedDocumentStatus.Completed, document.Status);
    }

    [Fact]
    public void RecordOcrResult_EmptyPageList_Throws()
    {
        var document = ScannedDocument.Create("CLM-1", "intake.pdf", "hash");

        Assert.Throws<ArgumentException>(() => document.RecordOcrResult([]));
    }

    [Fact]
    public void MarkFailed_SetsStatusAndErrorMessage()
    {
        var document = ScannedDocument.Create("CLM-1", "intake.pdf", "hash");

        document.MarkFailed("pdftoppm exited with code 1");

        Assert.Equal(ScannedDocumentStatus.Failed, document.Status);
        Assert.Equal("pdftoppm exited with code 1", document.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_EmptyMessage_Throws()
    {
        var document = ScannedDocument.Create("CLM-1", "intake.pdf", "hash");

        Assert.Throws<ArgumentException>(() => document.MarkFailed(""));
    }
}
