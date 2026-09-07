using System.Text.Json;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>The Exclusion Analyst agent (Claims Adjudication Guidelines, Step 3) — restricted to
/// knowledge-base search only.
///
/// It was originally given just the prior two agents' typed outputs, on the theory that it should
/// reason over their conclusions rather than re-read the raw claim. That was wrong, and it made the
/// agent unable to do its job: exclusions turn on the *circumstances* of the loss, and the anomaly
/// findings carry only boolean indicators, not what happened. Asked whether an exclusion applied,
/// the agent correctly answered that it could not tell -- "did not provide details about the cause
/// of loss ... without a description of the loss circumstances, I cannot confirm or rule out
/// exclusions" -- and every clean claim stopped at RequestMoreInfo with no payout. It now receives
/// the narrative and loss type, which is the minimum needed to reach a defensible conclusion.</summary>
public sealed class ExclusionAnalystAgent(AgentRunner runner, IPromptRepository prompts, SearchKnowledgeBaseToolExecutor searchKnowledgeBase)
{
    private const int MaxIterations = 6;

    public async Task<AgentRunResult<ExclusionAnalysisResult>> RunAsync(
        CoverageMatchResult coverageMatch,
        AnomalyFindings anomalyFindings,
        string lossType,
        string narrative,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = await prompts.GetAsync("exclusion-analyst", cancellationToken);
        var userMessage = $"""
            Loss type: {lossType}
            Claim narrative (the circumstances of the loss, as reported):
            {narrative}

            Coverage Matcher result: {JsonSerializer.Serialize(coverageMatch, JsonOptions)}
            Anomaly Analyst findings: {JsonSerializer.Serialize(anomalyFindings, JsonOptions)}
            """;

        IReadOnlyList<IToolExecutor> tools = [searchKnowledgeBase];
        return await runner.RunAsync<ExclusionAnalysisResult>("ExclusionAnalyst", systemPrompt, userMessage, tools, MaxIterations, cancellationToken);
    }

    // camelCase — matches the field names shown in this agent's own prompt examples.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
