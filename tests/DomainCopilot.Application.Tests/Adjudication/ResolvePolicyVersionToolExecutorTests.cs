using DomainCopilot.Application.Adjudication;
using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Tests.Adjudication;

public class ResolvePolicyVersionToolExecutorTests
{
    private static Document CompletedPolicyForm(string formVersion, DateOnly effectiveDate)
    {
        var document = Document.Create(
            $"policy_wording_{formVersion}", $"Policy Wording — {formVersion}", DocumentCategory.PolicyForm,
            DocumentFormat.Pdf, "policy-forms/x.pdf", "hash", formVersion, effectiveDate);
        document.BeginProcessing();
        document.MarkCompleted(10);
        return document;
    }

    [Fact]
    public async Task Execute_DateAfterLatestVersion_ResolvesLatestVersion()
    {
        var repo = new FakeDocumentRepository();
        repo.Seed(CompletedPolicyForm("PAP-2024-STD", new DateOnly(2024, 1, 1)));
        repo.Seed(CompletedPolicyForm("PAP-2025-STD", new DateOnly(2025, 6, 1)));
        var executor = new ResolvePolicyVersionToolExecutor(repo);

        var result = await executor.ExecuteAsync("""{"dateOfLoss": "2025-09-01"}""");

        Assert.True(result.Success);
        Assert.Contains("PAP-2025-STD", result.ResultJson);
        Assert.Contains("2025-06-01", result.ResultJson);
    }

    [Fact]
    public async Task Execute_DateBeforeEarliestVersion_Fails()
    {
        var repo = new FakeDocumentRepository();
        repo.Seed(CompletedPolicyForm("PAP-2024-STD", new DateOnly(2024, 1, 1)));
        var executor = new ResolvePolicyVersionToolExecutor(repo);

        var result = await executor.ExecuteAsync("""{"dateOfLoss": "2023-01-01"}""");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Execute_InvalidDate_Fails()
    {
        var executor = new ResolvePolicyVersionToolExecutor(new FakeDocumentRepository());

        var result = await executor.ExecuteAsync("""{"dateOfLoss": "not-a-date"}""");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Execute_MissingArgument_Fails()
    {
        var executor = new ResolvePolicyVersionToolExecutor(new FakeDocumentRepository());

        var result = await executor.ExecuteAsync("{}");

        Assert.False(result.Success);
    }
}
