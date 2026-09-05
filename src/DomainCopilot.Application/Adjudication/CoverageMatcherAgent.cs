using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>The Coverage Matcher agent (Claims Adjudication Guidelines, Steps 1-2) — restricted to
/// exactly the three tools it needs: resolving the policy version, looking up Declarations facts,
/// and searching the knowledge base for citation text.</summary>
public sealed class CoverageMatcherAgent(
    AgentRunner runner,
    IPromptRepository prompts,
    ResolvePolicyVersionToolExecutor resolvePolicyVersion,
    LookupDeclarationsToolExecutor lookupDeclarations,
    SearchKnowledgeBaseToolExecutor searchKnowledgeBase)
{
    private const int MaxIterations = 8;

    public async Task<AgentRunResult<CoverageMatchResult>> RunAsync(
        string claimNumber, string policyNumber, DateOnly dateOfLoss, string lossType, CancellationToken cancellationToken = default)
    {
        var systemPrompt = await prompts.GetAsync("coverage-matcher", cancellationToken);
        var userMessage = $"Claim {claimNumber}, policy {policyNumber}, date of loss {dateOfLoss:yyyy-MM-dd}. Loss type: {lossType}.";

        IReadOnlyList<IToolExecutor> tools = [resolvePolicyVersion, lookupDeclarations, searchKnowledgeBase];
        return await runner.RunAsync<CoverageMatchResult>("CoverageMatcher", systemPrompt, userMessage, tools, MaxIterations, cancellationToken);
    }
}
