using DomainCopilot.Application.Retrieval;
using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Tests.Retrieval;

public class PolicyVersionResolverTests
{
    private static Document PolicyForm(string formVersion, DateOnly effectiveDate) => Document.Create(
        sourceId: $"policy_wording_{formVersion}",
        title: $"Policy Wording — {formVersion}",
        category: DocumentCategory.PolicyForm,
        format: DocumentFormat.Pdf,
        sourceFileName: "policy-forms/policy_wording.pdf",
        contentHash: "hash",
        formVersion: formVersion,
        effectiveDate: effectiveDate);

    private static readonly Document V1 = PolicyForm("PAP-2024-STD", new DateOnly(2024, 1, 1));
    private static readonly Document V2 = PolicyForm("PAP-2025-STD", new DateOnly(2025, 6, 1));

    [Fact]
    public void Resolve_DateAfterBothEffectiveDates_ReturnsLatestVersion()
    {
        var result = PolicyVersionResolver.Resolve([V1, V2], new DateOnly(2025, 12, 1));

        Assert.Equal("PAP-2025-STD", result);
    }

    [Fact]
    public void Resolve_DateBetweenTheTwoEffectiveDates_ReturnsEarlierVersion()
    {
        var result = PolicyVersionResolver.Resolve([V1, V2], new DateOnly(2024, 6, 1));

        Assert.Equal("PAP-2024-STD", result);
    }

    [Fact]
    public void Resolve_DateExactlyOnEffectiveDate_ReturnsThatVersion()
    {
        var result = PolicyVersionResolver.Resolve([V1, V2], new DateOnly(2025, 6, 1));

        Assert.Equal("PAP-2025-STD", result);
    }

    [Fact]
    public void Resolve_DateBeforeEarliestEffectiveDate_ReturnsNull()
    {
        var result = PolicyVersionResolver.Resolve([V1, V2], new DateOnly(2023, 1, 1));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_IgnoresNonPolicyFormDocuments()
    {
        var reference = Document.Create(
            "glossary", "Glossary", DocumentCategory.Reference, DocumentFormat.Pdf,
            "reference/glossary.pdf", "hash", formVersion: null, effectiveDate: new DateOnly(2020, 1, 1));

        var result = PolicyVersionResolver.Resolve([reference], new DateOnly(2025, 1, 1));

        Assert.Null(result);
    }
}
