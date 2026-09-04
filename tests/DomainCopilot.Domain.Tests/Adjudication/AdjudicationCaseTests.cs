using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Domain.Tests.Adjudication;

public class AdjudicationCaseTests
{
    private static AdjudicationCase CreateValid() =>
        AdjudicationCase.Create("CLM-2025-04417", "MMIC-PAP-100234", new DateOnly(2025, 8, 3));

    private static AdjudicationCase AtAwaitingApproval()
    {
        var acase = CreateValid();
        acase.BeginCoverageMatching();
        acase.RecordCoverageMatch("""{"formVersion":"PAP-2024-STD"}""");
        acase.RecordAnomalyFindings("""{"duplicateClaims":false}""");
        acase.RecordExclusionAnalysis("""{"exclusionsApply":false}""");
        acase.RecordRecommendation("""{"recommendation":"Approve","payout":2500}""");
        return acase;
    }

    [Fact]
    public void Create_ValidInput_StartsPending()
    {
        var acase = CreateValid();

        Assert.Equal(AdjudicationRunStatus.Pending, acase.Status);
        Assert.NotEqual(Guid.Empty, acase.Id);
    }

    [Fact]
    public void Create_EmptyClaimNumber_Throws()
    {
        Assert.Throws<ArgumentException>(() => AdjudicationCase.Create("", "MMIC-PAP-100234", new DateOnly(2025, 8, 3)));
    }

    [Fact]
    public void Create_EmptyPolicyNumber_Throws()
    {
        Assert.Throws<ArgumentException>(() => AdjudicationCase.Create("CLM-1", "", new DateOnly(2025, 8, 3)));
    }

    [Fact]
    public void FullPipeline_RunsThroughEveryStageInOrder_ToAwaitingApproval()
    {
        var acase = AtAwaitingApproval();

        Assert.Equal(AdjudicationRunStatus.AwaitingApproval, acase.Status);
        Assert.NotNull(acase.CoverageMatchResultJson);
        Assert.NotNull(acase.AnomalyFindingsJson);
        Assert.NotNull(acase.ExclusionAnalysisResultJson);
        Assert.NotNull(acase.RecommendationJson);
    }

    [Fact]
    public void RecordCoverageMatch_BeforeBeginCoverageMatching_Throws()
    {
        var acase = CreateValid();

        Assert.Throws<InvalidOperationException>(() => acase.RecordCoverageMatch("{}"));
    }

    [Fact]
    public void RecordAnomalyFindings_SkippingCoverageMatch_Throws()
    {
        var acase = CreateValid();
        acase.BeginCoverageMatching();

        Assert.Throws<InvalidOperationException>(() => acase.RecordAnomalyFindings("{}"));
    }

    [Fact]
    public void RecordExclusionAnalysis_SkippingAnomalyDetection_Throws()
    {
        var acase = CreateValid();
        acase.BeginCoverageMatching();
        acase.RecordCoverageMatch("{}");

        Assert.Throws<InvalidOperationException>(() => acase.RecordExclusionAnalysis("{}"));
    }

    [Fact]
    public void RecordRecommendation_SkippingExclusionAnalysis_Throws()
    {
        var acase = CreateValid();
        acase.BeginCoverageMatching();
        acase.RecordCoverageMatch("{}");
        acase.RecordAnomalyFindings("{}");

        Assert.Throws<InvalidOperationException>(() => acase.RecordRecommendation("{}"));
    }

    [Fact]
    public void RecordCoverageMatch_EmptyJson_Throws()
    {
        var acase = CreateValid();
        acase.BeginCoverageMatching();

        Assert.Throws<ArgumentException>(() => acase.RecordCoverageMatch("  "));
    }

    [Fact]
    public void Approve_FromAwaitingApproval_SetsApprovedStateAndAudit()
    {
        var acase = AtAwaitingApproval();

        acase.Approve("adjuster.jane");

        Assert.Equal(AdjudicationRunStatus.Approved, acase.Status);
        Assert.Equal("adjuster.jane", acase.ApprovedBy);
        Assert.NotNull(acase.ApprovedAtUtc);
        Assert.Null(acase.AdjusterComments);
    }

    [Fact]
    public void Approve_BeforeAwaitingApproval_Throws()
    {
        var acase = CreateValid();

        Assert.Throws<InvalidOperationException>(() => acase.Approve("adjuster.jane"));
    }

    [Fact]
    public void Reject_FromAwaitingApproval_RequiresAndRecordsReason()
    {
        var acase = AtAwaitingApproval();

        acase.Reject("adjuster.jane", "Insufficient documentation of the police report.");

        Assert.Equal(AdjudicationRunStatus.Rejected, acase.Status);
        Assert.Equal("Insufficient documentation of the police report.", acase.AdjusterComments);
    }

    [Fact]
    public void Reject_EmptyReason_Throws()
    {
        var acase = AtAwaitingApproval();

        Assert.Throws<ArgumentException>(() => acase.Reject("adjuster.jane", ""));
    }

    [Fact]
    public void EditAndApprove_ReplacesRecommendationAndRecordsComments()
    {
        var acase = AtAwaitingApproval();

        acase.EditAndApprove("adjuster.jane", """{"recommendation":"Approve","payout":2000}""", "Reduced payout after re-checking the estimate.");

        Assert.Equal(AdjudicationRunStatus.EditedAndApproved, acase.Status);
        Assert.Contains("2000", acase.RecommendationJson);
        Assert.Equal("Reduced payout after re-checking the estimate.", acase.AdjusterComments);
    }

    [Fact]
    public void MarkFailed_FromAnyNonTerminalStatus_Succeeds()
    {
        var acase = CreateValid();
        acase.BeginCoverageMatching();

        acase.MarkFailed("Completion provider unavailable after 3 retries.");

        Assert.Equal(AdjudicationRunStatus.Failed, acase.Status);
        Assert.Equal("Completion provider unavailable after 3 retries.", acase.FailureReason);
    }

    [Fact]
    public void MarkFailed_FromTerminalStatus_Throws()
    {
        var acase = AtAwaitingApproval();
        acase.Approve("adjuster.jane");

        Assert.Throws<InvalidOperationException>(() => acase.MarkFailed("too late"));
    }

    [Fact]
    public void MarkFailed_EmptyReason_Throws()
    {
        var acase = CreateValid();

        Assert.Throws<ArgumentException>(() => acase.MarkFailed(""));
    }
}
