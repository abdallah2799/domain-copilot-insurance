using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>The Anomaly Analyst agent (Claims Adjudication Guidelines, Section 3) — restricted to
/// the two deterministic-check tools; the two narrative-judgment indicators are its own reasoning
/// over the input it's given, not a tool call.</summary>
public sealed class AnomalyAnalystAgent(
    AgentRunner runner,
    IPromptRepository prompts,
    CheckDamageValueRatioToolExecutor checkDamageValueRatio,
    LookupClaimHistoryToolExecutor lookupClaimHistory)
{
    // Raised from an initial 6 after real verification against a local Ollama model showed it
    // genuinely needs more exploratory tool-call rounds than that for this agent's two-tool task.
    private const int MaxIterations = 12;

    public async Task<AgentRunResult<AnomalyFindings>> RunAsync(
        string claimNumber,
        string policyNumber,
        DateOnly dateOfLoss,
        string narrative,
        string? policeReport,
        decimal estimatedDamage,
        decimal approximateVehicleValue,
        CoverageMatchResult coverageMatch,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = await prompts.GetAsync("anomaly-analyst", cancellationToken);
        var userMessage = $"""
            Claim {claimNumber}, policy {policyNumber}, date of loss {dateOfLoss:yyyy-MM-dd}.
            Estimated damage: {estimatedDamage}. Approximate vehicle value: {approximateVehicleValue}.
            Narrative: {narrative}
            Police report: {policeReport ?? "None filed."}
            Coverage Matcher result: formVersion {coverageMatch.FormVersion}, effectiveDate {coverageMatch.FormVersionEffectiveDate:yyyy-MM-dd}, endorsementsHeld [{string.Join(", ", coverageMatch.EndorsementsHeld)}].
            """;

        IReadOnlyList<IToolExecutor> tools = [checkDamageValueRatio, lookupClaimHistory];
        return await runner.RunAsync<AnomalyFindings>("AnomalyAnalyst", systemPrompt, userMessage, tools, MaxIterations, cancellationToken);
    }
}
