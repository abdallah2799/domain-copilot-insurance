using DomainCopilot.Application.Adjudication;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class FinalizeAdjudicationDecisionToolExecutorTests
{
    private static async Task<(FakeAdjudicationCaseRepository Repo, AdjudicationCase Case)> AwaitingApprovalCaseAsync()
    {
        var acase = AdjudicationCase.Create("CLM-2025-04417", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), "test-user");
        acase.BeginCoverageMatching();
        acase.RecordCoverageMatch("{}");
        acase.RecordAnomalyFindings("{}");
        acase.RecordExclusionAnalysis("{}");
        acase.RecordRecommendation("""{"recommendation":"Approve","payout":2500}""");

        var repo = new FakeAdjudicationCaseRepository();
        await repo.AddAsync(acase);
        return (repo, acase);
    }

    [Fact]
    public async Task Execute_Approve_TransitionsToApproved()
    {
        var (repo, acase) = await AwaitingApprovalCaseAsync();
        var executor = new FinalizeAdjudicationDecisionToolExecutor(repo);

        var result = await executor.ExecuteAsync($$"""{"adjudicationCaseId": "{{acase.Id}}", "decision": "Approve", "actor": "adjuster.jane"}""");

        Assert.True(result.Success);
        Assert.Equal(AdjudicationRunStatus.Approved, acase.Status);
        Assert.Equal("adjuster.jane", acase.ApprovedBy);
    }

    [Fact]
    public async Task Execute_Reject_RequiresCommentsAndTransitionsToRejected()
    {
        var (repo, acase) = await AwaitingApprovalCaseAsync();
        var executor = new FinalizeAdjudicationDecisionToolExecutor(repo);

        var result = await executor.ExecuteAsync(
            $$"""{"adjudicationCaseId": "{{acase.Id}}", "decision": "Reject", "actor": "adjuster.jane", "comments": "Estimate looks inflated."}""");

        Assert.True(result.Success);
        Assert.Equal(AdjudicationRunStatus.Rejected, acase.Status);
        Assert.Equal("Estimate looks inflated.", acase.AdjusterComments);
    }

    [Fact]
    public async Task Execute_Reject_WithoutComments_FailsRatherThanSilentlyApplyingEmptyReason()
    {
        var (repo, acase) = await AwaitingApprovalCaseAsync();
        var executor = new FinalizeAdjudicationDecisionToolExecutor(repo);

        var result = await executor.ExecuteAsync($$"""{"adjudicationCaseId": "{{acase.Id}}", "decision": "Reject", "actor": "adjuster.jane"}""");

        Assert.False(result.Success);
        Assert.Equal(AdjudicationRunStatus.AwaitingApproval, acase.Status);
    }

    [Fact]
    public async Task Execute_EditAndApprove_ReplacesRecommendation()
    {
        var (repo, acase) = await AwaitingApprovalCaseAsync();
        var executor = new FinalizeAdjudicationDecisionToolExecutor(repo);

        var result = await executor.ExecuteAsync($$"""
            {"adjudicationCaseId": "{{acase.Id}}", "decision": "EditAndApprove", "actor": "adjuster.jane",
             "comments": "Reduced payout.", "editedRecommendationJson": "{\"payout\":2000}"}
            """);

        Assert.True(result.Success);
        Assert.Equal(AdjudicationRunStatus.EditedAndApproved, acase.Status);
        Assert.Contains("2000", acase.RecommendationJson);
    }

    [Fact]
    public async Task Execute_UnknownCaseId_Fails()
    {
        var executor = new FinalizeAdjudicationDecisionToolExecutor(new FakeAdjudicationCaseRepository());

        var result = await executor.ExecuteAsync(
            $$"""{"adjudicationCaseId": "{{Guid.NewGuid()}}", "decision": "Approve", "actor": "adjuster.jane"}""");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Execute_CaseNotYetAwaitingApproval_Fails()
    {
        var acase = AdjudicationCase.Create("CLM-2025-04417", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), "test-user");
        var repo = new FakeAdjudicationCaseRepository();
        await repo.AddAsync(acase);
        var executor = new FinalizeAdjudicationDecisionToolExecutor(repo);

        var result = await executor.ExecuteAsync($$"""{"adjudicationCaseId": "{{acase.Id}}", "decision": "Approve", "actor": "adjuster.jane"}""");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Execute_InvalidGuid_Fails()
    {
        var executor = new FinalizeAdjudicationDecisionToolExecutor(new FakeAdjudicationCaseRepository());

        var result = await executor.ExecuteAsync("""{"adjudicationCaseId": "not-a-guid", "decision": "Approve", "actor": "adjuster.jane"}""");

        Assert.False(result.Success);
        Assert.Contains("GUID", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_UnrecognizedDecision_Fails()
    {
        var (repo, acase) = await AwaitingApprovalCaseAsync();
        var executor = new FinalizeAdjudicationDecisionToolExecutor(repo);

        var result = await executor.ExecuteAsync($$"""{"adjudicationCaseId": "{{acase.Id}}", "decision": "Approve-ish", "actor": "adjuster.jane"}""");

        Assert.False(result.Success);
    }
}
