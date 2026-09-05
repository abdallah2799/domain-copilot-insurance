using System.Text.Json;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>
/// The Adjudication Drafter agent (Claims Adjudication Guidelines, Steps 4-5) — restricted to the
/// four deterministic payout tools plus knowledge-base search. Deliberately does NOT include
/// <c>finalize_adjudication_decision</c>: <see cref="AgentRunner"/> executes any tool call it
/// receives without an approval-gate special case, so including the write tool here would let the
/// model trigger it during an ordinary drafting turn, before any human has approved anything. That
/// tool is only ever invoked directly by the API's approval-gate endpoints, never through an
/// agent's own tool-calling loop.
/// </summary>
public sealed class AdjudicationDrafterAgent(
    AgentRunner runner,
    IPromptRepository prompts,
    StandardPayoutToolExecutor standardPayout,
    TotalLossDeterminationToolExecutor totalLossDetermination,
    TotalLossSettlementToolExecutor totalLossSettlement,
    GapCoverageToolExecutor gapCoverage,
    SearchKnowledgeBaseToolExecutor searchKnowledgeBase)
{
    private const int MaxIterations = 10;

    public async Task<AgentRunResult<Recommendation>> RunAsync(
        CoverageMatchResult coverageMatch,
        AnomalyFindings anomalyFindings,
        ExclusionAnalysisResult exclusionAnalysis,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = await prompts.GetAsync("adjudication-drafter", cancellationToken);
        var userMessage = $"""
            Coverage Matcher result: {JsonSerializer.Serialize(coverageMatch, JsonOptions)}
            Anomaly Analyst findings: {JsonSerializer.Serialize(anomalyFindings, JsonOptions)}
            Exclusion Analyst result: {JsonSerializer.Serialize(exclusionAnalysis, JsonOptions)}
            """;

        IReadOnlyList<IToolExecutor> tools = [standardPayout, totalLossDetermination, totalLossSettlement, gapCoverage, searchKnowledgeBase];
        return await runner.RunAsync<Recommendation>("AdjudicationDrafter", systemPrompt, userMessage, tools, MaxIterations, cancellationToken);
    }

    // camelCase — matches the field names shown in this agent's own prompt examples.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
