using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Retrieval;
using DomainCopilot.Domain.Adjudication;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Adjudication;

/// <summary>
/// The pipeline/state-machine orchestrator (FR-5, ADR-0009) driving one <see cref="AdjudicationCase"/>
/// through Coverage Matcher → Anomaly Analyst → Exclusion Analyst → Adjudication Drafter → the
/// approval gate. A fixed sequence, not an LLM-driven planner: the orchestrator decides what runs
/// next, never the model. Per-step timeout is enforced here (a linked, canceled-after token per
/// stage); the max-iteration breaker and retry-with-backoff live inside <see cref="AgentRunner"/>,
/// one level down, since those are per-completion-call concerns each agent already owns.
/// </summary>
public sealed class AdjudicationOrchestrator(
    IAdjudicationCaseRepository caseRepository,
    CoverageMatcherAgent coverageMatcher,
    AnomalyAnalystAgent anomalyAnalyst,
    ExclusionAnalystAgent exclusionAnalyst,
    AdjudicationDrafterAgent adjudicationDrafter,
    HybridRetrievalService retrievalService,
    ICompletionService completionService,
    ILogger<AdjudicationOrchestrator> logger)
{
    // A real local-model run (measured against this project's own agent prompts, multiple
    // tool-calling round trips per stage) genuinely took several minutes per stage on modest
    // hardware — a raw single tool-enabled call measured ~82s, and this project's actual prompts
    // (full XML structure, 3 few-shot examples) are substantially longer than that probe, so a
    // multi-call agent turn needs real headroom. A hosted provider would comfortably clear this in
    // a fraction of the time; this generous value is specifically for local-model verification.
    private static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(20);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdjudicationCase> RunAsync(StartAdjudicationRequest request, CancellationToken cancellationToken = default)
    {
        var adjudicationCase = AdjudicationCase.Create(request.ClaimNumber, request.PolicyNumber, request.DateOfLoss);
        await caseRepository.AddAsync(adjudicationCase, cancellationToken);
        await caseRepository.SaveChangesAsync(cancellationToken);

        adjudicationCase.BeginCoverageMatching();
        await caseRepository.SaveChangesAsync(cancellationToken);

        var coverageMatch = await RunStepAsync(
            adjudicationCase, "CoverageMatcher", request.Narrative, cancellationToken,
            ct => coverageMatcher.RunAsync(request.ClaimNumber, request.PolicyNumber, request.DateOfLoss, request.LossType, ct));
        if (coverageMatch is null)
        {
            return adjudicationCase;
        }

        adjudicationCase.RecordCoverageMatch(JsonSerializer.Serialize(coverageMatch, JsonOptions));
        await caseRepository.SaveChangesAsync(cancellationToken);

        var anomalyFindings = await RunStepAsync(
            adjudicationCase, "AnomalyAnalyst", request.Narrative, cancellationToken,
            ct => anomalyAnalyst.RunAsync(
                request.ClaimNumber, request.PolicyNumber, request.DateOfLoss, request.Narrative, request.PoliceReportText,
                request.EstimatedDamage, request.ApproximateVehicleValue, coverageMatch, ct));
        if (anomalyFindings is null)
        {
            return adjudicationCase;
        }

        adjudicationCase.RecordAnomalyFindings(JsonSerializer.Serialize(anomalyFindings, JsonOptions));
        await caseRepository.SaveChangesAsync(cancellationToken);

        var exclusionAnalysis = await RunStepAsync(
            adjudicationCase, "ExclusionAnalyst", request.Narrative, cancellationToken,
            ct => exclusionAnalyst.RunAsync(coverageMatch, anomalyFindings, ct));
        if (exclusionAnalysis is null)
        {
            return adjudicationCase;
        }

        adjudicationCase.RecordExclusionAnalysis(JsonSerializer.Serialize(exclusionAnalysis, JsonOptions));
        await caseRepository.SaveChangesAsync(cancellationToken);

        var recommendation = await RunStepAsync(
            adjudicationCase, "AdjudicationDrafter", request.Narrative, cancellationToken,
            ct => adjudicationDrafter.RunAsync(coverageMatch, anomalyFindings, exclusionAnalysis, ct));
        if (recommendation is null)
        {
            return adjudicationCase;
        }

        adjudicationCase.RecordRecommendation(JsonSerializer.Serialize(recommendation, JsonOptions));
        await caseRepository.SaveChangesAsync(cancellationToken);

        return adjudicationCase;
    }

    /// <summary>Runs one stage under a per-step timeout; on failure, degrades to a plain-RAG summary
    /// (FR-5) rather than leaving the case with nothing but an exception message, then marks the
    /// case Failed and returns null so the caller stops advancing the pipeline.</summary>
    private async Task<T?> RunStepAsync<T>(
        AdjudicationCase adjudicationCase,
        string stageName,
        string narrativeContext,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<AgentRunResult<T>>> step)
        where T : class
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(StepTimeout);

        AgentRunResult<T> result;
        try
        {
            result = await step(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = AgentRunResult<T>.Failed($"{stageName} exceeded its {StepTimeout.TotalMinutes}-minute step timeout.");
        }

        if (result.Success)
        {
            return result.Output;
        }

        logger.LogWarning("{Stage} failed for case {CaseId}: {Error}", stageName, adjudicationCase.Id, result.ErrorMessage);
        var degraded = await DegradeToPlainRagAsync(stageName, narrativeContext, result.ErrorMessage!, cancellationToken);
        adjudicationCase.MarkFailed(degraded);
        await caseRepository.SaveChangesAsync(cancellationToken);
        return null;
    }

    /// <summary>The graceful-degrade path (FR-5): a plain retrieval + single ungated completion
    /// call, clearly labeled as a fallback rather than a structured recommendation, so a run that
    /// can't complete automated adjudication still leaves the adjuster something to act on instead
    /// of a bare error message.</summary>
    private async Task<string> DegradeToPlainRagAsync(string stageName, string narrativeContext, string originalError, CancellationToken cancellationToken)
    {
        try
        {
            var retrieval = await retrievalService.SearchAsync(new RetrievalQuery(narrativeContext, TopK: 5), cancellationToken);
            var citedText = string.Join("\n\n", retrieval.Chunks.Select(c => $"[{c.DocumentTitle} — {c.SectionTitle}] {c.Text}"));

            var summary = await completionService.CompleteAsync(new CompletionRequest([
                ChatMessage.System(
                    "You are a claims assistant producing a fallback summary because automated adjudication could not complete. " +
                    "Summarize in 2-3 plain sentences what the retrieved policy text below says that's relevant to the situation. " +
                    "Do not compute any payout figure and do not state a coverage determination — this is not a recommendation, " +
                    "only a starting point for a human adjuster who must now review this claim manually."),
                ChatMessage.User($"Situation: {narrativeContext}\n\nRetrieved text:\n{citedText}"),
            ]), cancellationToken);

            return $"[DEGRADED — {stageName} could not complete ({originalError}). Fallback summary: {summary.Content}]";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Graceful-degrade fallback itself failed for stage {Stage}", stageName);
            return $"[DEGRADED — {stageName} could not complete ({originalError}), and the fallback summary also failed: {ex.Message}]";
        }
    }
}
