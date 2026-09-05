using System.Text.Json;
using DomainCopilot.Application.Adjudication;
using DomainCopilot.Domain.Adjudication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DomainCopilot.Api.Controllers;

/// <summary>
/// FR-5's surface: starting a run, inspecting it step-by-step by run ID, and the approval gate
/// (approve/reject/edit-and-approve). The approval endpoints call
/// <see cref="FinalizeAdjudicationDecisionToolExecutor"/> directly — never through an agent's own
/// tool-calling loop, per the reason documented on <see cref="AdjudicationDrafterAgent"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AdjudicationController(
    AdjudicationOrchestrator orchestrator,
    IAdjudicationCaseRepository caseRepository,
    FinalizeAdjudicationDecisionToolExecutor finalizeDecision,
    IServiceScopeFactory scopeFactory,
    ILogger<AdjudicationController> logger) : ControllerBase
{
    private static readonly HashSet<AdjudicationRunStatus> TerminalStatuses =
    [
        AdjudicationRunStatus.Approved, AdjudicationRunStatus.Rejected,
        AdjudicationRunStatus.EditedAndApproved, AdjudicationRunStatus.Failed,
    ];

    // GetRunStream's own stop condition is broader than TerminalStatuses: once a case reaches
    // AwaitingApproval, the four-agent pipeline itself has nothing left to report -- the only thing
    // left is a human's decision, which this stream isn't the mechanism for. Stopping there too
    // (not just at the four hard-terminal statuses) avoids leaving an SSE connection, and the
    // server-side poll loop behind it, open indefinitely for however long a human takes to act.
    private static readonly HashSet<AdjudicationRunStatus> PipelineInProgressStatuses =
    [
        AdjudicationRunStatus.Pending, AdjudicationRunStatus.MatchingCoverage,
        AdjudicationRunStatus.DetectingAnomalies, AdjudicationRunStatus.AnalyzingExclusions,
        AdjudicationRunStatus.Drafting,
    ];

    // Matches RetrievalController's own SSE heartbeat cadence for the same reason -- see
    // GetRunStream's remark below on why a long, genuinely-computing silence needs one here too.
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    // How often GetRunStream re-reads the case row while a run is in progress. There's no
    // in-process event bus wiring AdjudicationOrchestrator's stage transitions directly to this
    // endpoint (each stage already persists to the same table any GET already reads) -- this
    // moves that polling server-side, behind a single SSE connection, rather than the client
    // re-issuing an HTTP GET on its own timer, but it is still polling underneath, not push
    // triggered by the orchestrator itself. Documented here rather than implied as more than it is.
    private static readonly TimeSpan ProgressPollInterval = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>FR-6's "watch it start immediately": creates the case synchronously (so the
    /// response carries a real id right away) and runs the four-agent pipeline in the background
    /// against its own DI scope, independent of this request's lifetime -- the pipeline keeps
    /// running even after this response returns, and (deliberately, see the class-level remark
    /// below) even if the client that started it disconnects. Watch progress via
    /// <see cref="GetRunStream"/>, or poll <see cref="GetRun"/>.</summary>
    [HttpPost("runs")]
    public async Task<ActionResult<AdjudicationCase>> StartRun([FromBody] StartAdjudicationRequest request, CancellationToken cancellationToken)
    {
        var adjudicationCase = await orchestrator.StartCaseAsync(request, cancellationToken);

        // Deliberately CancellationToken.None, not this request's token: unlike the ask/stream
        // endpoint (FR-6's other half, where cancellation is real and desired), an adjudication
        // run is meant to keep going once started regardless of whether the browser tab that
        // triggered it stays open -- an adjuster elsewhere should still see it complete. A
        // separate "cancel this specific run" capability (a cancellation-token registry keyed by
        // case id, plus its own endpoint) would be needed for genuine mid-run cancellation here,
        // and is intentionally not built in this round -- tracked as a follow-up, not silently
        // implied as covered by this change.
        _ = RunPipelineInBackgroundAsync(adjudicationCase.Id, request);

        return Ok(adjudicationCase);
    }

    private async Task RunPipelineInBackgroundAsync(Guid caseId, StartAdjudicationRequest request)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedOrchestrator = scope.ServiceProvider.GetRequiredService<AdjudicationOrchestrator>();
        var scopedCaseRepository = scope.ServiceProvider.GetRequiredService<IAdjudicationCaseRepository>();

        try
        {
            var adjudicationCase = await scopedCaseRepository.FindByIdAsync(caseId, CancellationToken.None)
                ?? throw new InvalidOperationException($"Adjudication case {caseId} was not found immediately after creation.");
            await scopedOrchestrator.RunPipelineAsync(adjudicationCase, request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // AdjudicationOrchestrator.RunPipelineAsync already catches and degrades every
            // per-stage failure it can anticipate (RunStepAsync) -- reaching here means something
            // outside that (a DI resolution failure, an unhandled bug) blew past all of it. A
            // fire-and-forget Task's unhandled exception has nowhere else to go, so this is the
            // last chance to both log it and leave the case in an honest terminal state instead of
            // stuck showing "MatchingCoverage" forever with no explanation.
            logger.LogError(ex, "Adjudication run {CaseId} failed outside the per-stage degrade path.", caseId);
            try
            {
                var adjudicationCase = await scopedCaseRepository.FindByIdAsync(caseId, CancellationToken.None);
                if (adjudicationCase is not null && !TerminalStatuses.Contains(adjudicationCase.Status))
                {
                    adjudicationCase.MarkFailed($"[DEGRADED — unexpected error outside the per-stage degrade path: {ex.Message}]");
                    await scopedCaseRepository.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception markFailedEx)
            {
                logger.LogError(markFailedEx, "Failed to mark case {CaseId} Failed after an unexpected error.", caseId);
            }
        }
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<AdjudicationCase>> GetRun(Guid id, CancellationToken cancellationToken)
    {
        var adjudicationCase = await caseRepository.FindByIdAsync(id, cancellationToken);
        return adjudicationCase is null ? NotFound() : Ok(adjudicationCase);
    }

    /// <summary>FR-6's live per-agent progress feed: one SSE "update" event each time the case row
    /// actually changes (see <see cref="ProgressPollInterval"/> for what "checks" really means
    /// here), ending with one final event once the run reaches a terminal status. A GET, not a
    /// POST, since there's no request body to send and the browser's native <c>EventSource</c>
    /// (GET-only) works fine for this one, unlike ask/stream.</summary>
    [HttpGet("runs/{id:guid}/stream")]
    public async Task GetRunStream(Guid id, CancellationToken cancellationToken)
    {
        var initial = await caseRepository.FindByIdAsync(id, cancellationToken);
        if (initial is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            await WriteUpdateAsync(initial, cancellationToken);
            if (!PipelineInProgressStatuses.Contains(initial.Status))
            {
                return;
            }

            // Real live evidence, not a hypothetical: verifying this endpoint against a real run
            // that fell back to local Ollama showed several minutes with the case genuinely
            // computing (confirmed via the Ollama process's own CPU usage) but zero DB row changes
            // to report -- the same "long silent gap looks like a dead connection" issue found on
            // ask/stream, for the same reason (a real agent call just takes a while), so it gets
            // the same fix: a heartbeat comment on any poll tick where nothing actually changed.
            AdjudicationCase? previous = initial;
            var lastWriteUtc = DateTimeOffset.UtcNow;
            while (true)
            {
                await Task.Delay(ProgressPollInterval, cancellationToken);

                var current = await caseRepository.FindByIdAsync(id, cancellationToken);
                if (current is null)
                {
                    return;
                }

                if (current.Status != previous!.Status || current.UpdatedAtUtc != previous.UpdatedAtUtc)
                {
                    await WriteUpdateAsync(current, cancellationToken);
                    lastWriteUtc = DateTimeOffset.UtcNow;
                }
                else if (DateTimeOffset.UtcNow - lastWriteUtc >= HeartbeatInterval)
                {
                    await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    lastWriteUtc = DateTimeOffset.UtcNow;
                }

                if (!PipelineInProgressStatuses.Contains(current.Status))
                {
                    return;
                }

                previous = current;
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // The client navigated away or closed the tab -- nothing left to write to, and the
            // background pipeline run itself is deliberately unaffected (see StartRun's remark).
        }
    }

    private async Task WriteUpdateAsync(AdjudicationCase adjudicationCase, CancellationToken cancellationToken)
    {
        await Response.WriteAsync("event: update\n", cancellationToken);
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(adjudicationCase, SseJsonOptions)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<AdjudicationCase>>> ListRuns(CancellationToken cancellationToken) =>
        Ok(await caseRepository.ListAllAsync(cancellationToken));

    [HttpPost("runs/{id:guid}/approve")]
    public Task<ActionResult> Approve(Guid id, [FromBody] ApprovalRequest request, CancellationToken cancellationToken) =>
        FinalizeAsync(id, "Approve", request.Actor, comments: null, editedRecommendationJson: null, cancellationToken);

    [HttpPost("runs/{id:guid}/reject")]
    public Task<ActionResult> Reject(Guid id, [FromBody] ApprovalRequest request, CancellationToken cancellationToken) =>
        FinalizeAsync(id, "Reject", request.Actor, request.Comments, editedRecommendationJson: null, cancellationToken);

    [HttpPost("runs/{id:guid}/edit-and-approve")]
    public Task<ActionResult> EditAndApprove(Guid id, [FromBody] EditAndApproveRequest request, CancellationToken cancellationToken) =>
        FinalizeAsync(id, "EditAndApprove", request.Actor, request.Comments, request.EditedRecommendationJson, cancellationToken);

    private async Task<ActionResult> FinalizeAsync(
        Guid id, string decision, string actor, string? comments, string? editedRecommendationJson, CancellationToken cancellationToken)
    {
        var argumentsJson = JsonSerializer.Serialize(new
        {
            adjudicationCaseId = id,
            decision,
            actor,
            comments,
            editedRecommendationJson,
        });

        var result = await finalizeDecision.ExecuteAsync(argumentsJson, cancellationToken);
        return result.Success ? Ok(JsonSerializer.Deserialize<JsonElement>(result.ResultJson!)) : BadRequest(result.ErrorMessage);
    }

    public sealed record ApprovalRequest(string Actor, string? Comments);

    public sealed record EditAndApproveRequest(string Actor, string Comments, string EditedRecommendationJson);
}
