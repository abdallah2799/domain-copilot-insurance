using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

/// <summary>
/// The deterministic payout guarantee is only worth anything if the tool demonstrably ran. These
/// cover the case a real run actually produced: a recommendation reporting
/// payoutToolUsed "calculate_standard_payout" with a figure the model had worked out itself, having
/// never called that tool. A wrong number wearing the guardrail's name is worse than an obviously
/// invented one, because it survives review.
/// </summary>
public class PayoutAttributionVerifierTests
{
    private static Recommendation Recommendation(decimal? payout, string? tool) =>
        new("Approve", payout, tool, "summary", ["citation"]);

    private static AgentRunResult<Recommendation> RunWith(Recommendation recommendation, params ExecutedToolCall[] executed) =>
        AgentRunResult<Recommendation>.Ok(recommendation, iterationsUsed: 2, executed);

    private static ExecutedToolCall StandardPayoutReturning(decimal payout) =>
        new("calculate_standard_payout", $$"""{"payout":{{payout}}}""");

    [Fact]
    public void PayoutReportedButToolNeverCalled_IsRejected()
    {
        var result = PayoutAttributionVerifier.Verify(
            RunWith(Recommendation(2700m, "calculate_standard_payout")));

        Assert.False(result.Success);
        Assert.Contains("never called that tool", result.ErrorMessage);
    }

    [Fact]
    public void PayoutDisagreesWithWhatTheToolReturned_IsRejected()
    {
        var result = PayoutAttributionVerifier.Verify(
            RunWith(Recommendation(2700m, "calculate_standard_payout"), StandardPayoutReturning(3700m)));

        Assert.False(result.Success);
        Assert.Contains("that tool returned 3700", result.ErrorMessage);
    }

    [Fact]
    public void PayoutMatchesTheToolsActualResult_IsAccepted()
    {
        var result = PayoutAttributionVerifier.Verify(
            RunWith(Recommendation(3700m, "calculate_standard_payout"), StandardPayoutReturning(3700m)));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3700m, result.Output!.PayoutAmount);
    }

    [Fact]
    public void PayoutAttributedToANonPayoutTool_IsRejected()
    {
        var result = PayoutAttributionVerifier.Verify(
            RunWith(Recommendation(2700m, "search_knowledge_base"),
                new ExecutedToolCall("search_knowledge_base", """{"chunks":[]}""")));

        Assert.False(result.Success);
        Assert.Contains("not one of the deterministic payout tools", result.ErrorMessage);
    }

    [Fact]
    public void PayoutWithNoToolNamedAtAll_IsRejected()
    {
        var result = PayoutAttributionVerifier.Verify(RunWith(Recommendation(2700m, null)));

        Assert.False(result.Success);
        Assert.Contains("(none)", result.ErrorMessage);
    }

    // A denial or a request for more information legitimately carries no payout, and must pass
    // through untouched -- the guardrail must not turn a valid "Deny" into a failed run.
    [Fact]
    public void NoPayoutReported_SkipsVerification()
    {
        var result = PayoutAttributionVerifier.Verify(
            AgentRunResult<Recommendation>.Ok(new Recommendation("Deny", null, null, "Coverage not held.", ["c"]), 1));

        Assert.True(result.Success);
        Assert.Null(result.Output!.PayoutAmount);
    }

    [Fact]
    public void TotalLossSettlementAndGapCoverageAreAlsoAcceptedSources()
    {
        var settlement = PayoutAttributionVerifier.Verify(
            RunWith(Recommendation(14500m, "calculate_total_loss_settlement"),
                new ExecutedToolCall("calculate_total_loss_settlement", """{"settlement":14500}""")));
        var gap = PayoutAttributionVerifier.Verify(
            RunWith(Recommendation(2200m, "calculate_gap_coverage"),
                new ExecutedToolCall("calculate_gap_coverage", """{"gapPayout":2200}""")));

        Assert.True(settlement.Success, settlement.ErrorMessage);
        Assert.True(gap.Success, gap.ErrorMessage);
    }

    // The Drafter may legitimately call the payout tool more than once (e.g. correcting arguments);
    // matching any execution is enough, so a superseded earlier call doesn't fail a correct answer.
    [Fact]
    public void MatchesAnyExecutionWhenTheToolWasCalledMoreThanOnce()
    {
        var result = PayoutAttributionVerifier.Verify(
            RunWith(Recommendation(3700m, "calculate_standard_payout"),
                StandardPayoutReturning(999m), StandardPayoutReturning(3700m)));

        Assert.True(result.Success, result.ErrorMessage);
    }

    // A failed run must stay failed rather than acquiring a spurious verification error on top.
    [Fact]
    public void AlreadyFailedRun_PassesThroughUnchanged()
    {
        var failed = AgentRunResult<Recommendation>.Failed("exceeded max iterations");

        var result = PayoutAttributionVerifier.Verify(failed);

        Assert.Same(failed, result);
    }
}
