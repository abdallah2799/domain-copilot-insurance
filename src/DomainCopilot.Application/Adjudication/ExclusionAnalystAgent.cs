using System.Text.Json;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>The Exclusion Analyst agent (Claims Adjudication Guidelines, Step 3) — restricted to
/// knowledge-base search only; it reasons over the prior two agents' typed outputs rather than
/// re-reading the raw claim narrative itself (the design review's key simplification).</summary>
public sealed class ExclusionAnalystAgent(AgentRunner runner, IPromptRepository prompts, SearchKnowledgeBaseToolExecutor searchKnowledgeBase)
{
    private const int MaxIterations = 6;

    public async Task<AgentRunResult<ExclusionAnalysisResult>> RunAsync(
        CoverageMatchResult coverageMatch, AnomalyFindings anomalyFindings, CancellationToken cancellationToken = default)
    {
        var systemPrompt = await prompts.GetAsync("exclusion-analyst", cancellationToken);
        var userMessage = $"""
            Coverage Matcher result: {JsonSerializer.Serialize(coverageMatch, JsonOptions)}
            Anomaly Analyst findings: {JsonSerializer.Serialize(anomalyFindings, JsonOptions)}
            """;

        IReadOnlyList<IToolExecutor> tools = [searchKnowledgeBase];
        return await runner.RunAsync<ExclusionAnalysisResult>("ExclusionAnalyst", systemPrompt, userMessage, tools, MaxIterations, cancellationToken);
    }

    // camelCase — matches the field names shown in this agent's own prompt examples.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
