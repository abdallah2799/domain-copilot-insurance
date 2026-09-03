using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Domain.Tests.Documents;

public class DocumentTests
{
    private static Document CreateValid() => Document.Create(
        sourceId: "policy_wording_v1",
        title: "Policy Wording — PAP-2024-STD",
        category: DocumentCategory.PolicyForm,
        format: DocumentFormat.Pdf,
        sourceFileName: "policy-forms/policy_wording_v1.pdf",
        contentHash: "abc123",
        formVersion: "PAP-2024-STD");

    [Fact]
    public void Create_WithBlankSourceId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Document.Create(
            sourceId: "  ",
            title: "t",
            category: DocumentCategory.Reference,
            format: DocumentFormat.Pdf,
            sourceFileName: "f.pdf",
            contentHash: "hash"));
    }

    [Fact]
    public void Create_WithBlankContentHash_Throws()
    {
        Assert.Throws<ArgumentException>(() => Document.Create(
            sourceId: "id",
            title: "t",
            category: DocumentCategory.Reference,
            format: DocumentFormat.Pdf,
            sourceFileName: "f.pdf",
            contentHash: ""));
    }

    [Fact]
    public void Create_SetsStatusToPending()
    {
        var doc = CreateValid();
        Assert.Equal(IngestionStatus.Pending, doc.Status);
        Assert.Null(doc.IngestedAtUtc);
    }

    [Fact]
    public void NeedsReingestion_WithSameHash_ReturnsFalse()
    {
        var doc = CreateValid();
        Assert.False(doc.NeedsReingestion("abc123"));
    }

    [Fact]
    public void NeedsReingestion_WithDifferentHash_ReturnsTrue()
    {
        var doc = CreateValid();
        Assert.True(doc.NeedsReingestion("different-hash"));
    }

    [Fact]
    public void MarkCompleted_SetsIngestedAtAndClearsError()
    {
        var doc = CreateValid();
        doc.BeginProcessing();
        doc.MarkFailed("transient network error");
        doc.BeginProcessing();
        doc.MarkCompleted();

        Assert.Equal(IngestionStatus.Completed, doc.Status);
        Assert.Null(doc.ErrorMessage);
        Assert.NotNull(doc.IngestedAtUtc);
    }

    [Fact]
    public void MarkFailed_WithBlankMessage_Throws()
    {
        var doc = CreateValid();
        Assert.Throws<ArgumentException>(() => doc.MarkFailed(""));
    }

    [Fact]
    public void MarkFailed_RecordsStatusAndMessage_AndLeavesIngestedAtUnset()
    {
        var doc = CreateValid();
        doc.BeginProcessing();
        doc.MarkFailed("OCR engine unavailable");

        Assert.Equal(IngestionStatus.Failed, doc.Status);
        Assert.Equal("OCR engine unavailable", doc.ErrorMessage);
        Assert.Null(doc.IngestedAtUtc);
    }

    [Fact]
    public void UpdateContent_ChangesHashAndFormVersion_PreservesIdentity()
    {
        var doc = CreateValid();
        var originalId = doc.Id;
        var originalSourceId = doc.SourceId;

        doc.UpdateContent("Policy Wording — PAP-2025-STD", "new-hash", "PAP-2025-STD");

        Assert.Equal(originalId, doc.Id);
        Assert.Equal(originalSourceId, doc.SourceId);
        Assert.Equal("new-hash", doc.ContentHash);
        Assert.Equal("PAP-2025-STD", doc.FormVersion);
    }
}
