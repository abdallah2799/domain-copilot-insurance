using System.Text.Json;
using DomainCopilot.Application.Adjudication;
using DomainCopilot.Domain.Adjudication;
using Microsoft.AspNetCore.Mvc;

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
    FinalizeAdjudicationDecisionToolExecutor finalizeDecision) : ControllerBase
{
    [HttpPost("runs")]
    public async Task<ActionResult<AdjudicationCase>> StartRun([FromBody] StartAdjudicationRequest request, CancellationToken cancellationToken)
    {
        var result = await orchestrator.RunAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<AdjudicationCase>> GetRun(Guid id, CancellationToken cancellationToken)
    {
        var adjudicationCase = await caseRepository.FindByIdAsync(id, cancellationToken);
        return adjudicationCase is null ? NotFound() : Ok(adjudicationCase);
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
