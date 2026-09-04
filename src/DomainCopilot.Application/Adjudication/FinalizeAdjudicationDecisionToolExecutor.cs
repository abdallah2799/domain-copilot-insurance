using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>
/// The write/side-effecting tool FR-4 requires (≥1 of the ≥4 tools) — commits an adjuster's
/// approve/reject/edit-and-approve decision onto an <see cref="AdjudicationCase"/>. Declared in the
/// Adjudication Drafter's tool set like any other tool, but its execution is never triggered by an
/// LLM's own turn: the orchestrator only calls <see cref="ExecuteAsync"/> once a human adjuster has
/// actually acted via the API, the same "never auto-invoked, always Application-gated" discipline
/// <c>KernelToolMapper</c> already applies to every declared tool — this is simply the one tool
/// where that gate is load-bearing rather than a formality, since this is the only tool in the
/// system that mutates persistent state.
/// </summary>
public sealed class FinalizeAdjudicationDecisionToolExecutor(IAdjudicationCaseRepository repository) : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "finalize_adjudication_decision",
        "Commits an adjuster's decision (approve, reject, or edit-and-approve) on a drafted recommendation. Never call this yourself — it is only ever executed by the orchestrator after a human adjuster has actually approved, rejected, or edited the recommendation via the approval gate.",
        """
        {
          "type": "object",
          "properties": {
            "adjudicationCaseId": { "type": "string", "format": "uuid" },
            "decision": { "type": "string", "enum": ["Approve", "Reject", "EditAndApprove"] },
            "actor": { "type": "string", "description": "The adjuster's identity." },
            "comments": { "type": "string", "description": "Required for Reject and EditAndApprove; ignored for Approve." },
            "editedRecommendationJson": { "type": "string", "description": "Required for EditAndApprove: the adjuster's edited recommendation, replacing the drafted one." }
          },
          "required": ["adjudicationCaseId", "decision", "actor"]
        }
        """);

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        Guid adjudicationCaseId;
        string decision;
        string actor;
        string? comments;
        string? editedRecommendationJson;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            var idString = ToolArguments.RequireString(root, "adjudicationCaseId");
            if (!Guid.TryParse(idString, out adjudicationCaseId))
            {
                return ToolExecutionResult.Failed("Argument 'adjudicationCaseId' must be a valid GUID.");
            }

            decision = ToolArguments.RequireString(root, "decision");
            actor = ToolArguments.RequireString(root, "actor");
            comments = root.TryGetProperty("comments", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            editedRecommendationJson = root.TryGetProperty("editedRecommendationJson", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.Failed($"Invalid arguments: {ex.Message}");
        }
        catch (ToolArgumentException ex)
        {
            return ToolExecutionResult.Failed(ex.Message);
        }

        var adjudicationCase = await repository.FindByIdAsync(adjudicationCaseId, cancellationToken);
        if (adjudicationCase is null)
        {
            return ToolExecutionResult.Failed($"No adjudication case found with id '{adjudicationCaseId}'.");
        }

        try
        {
            switch (decision)
            {
                case "Approve":
                    adjudicationCase.Approve(actor);
                    break;
                case "Reject":
                    adjudicationCase.Reject(actor, comments ?? string.Empty);
                    break;
                case "EditAndApprove":
                    adjudicationCase.EditAndApprove(actor, editedRecommendationJson ?? string.Empty, comments ?? string.Empty);
                    break;
                default:
                    return ToolExecutionResult.Failed($"Argument 'decision' must be one of Approve, Reject, EditAndApprove — got '{decision}'.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ToolExecutionResult.Failed(ex.Message);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return ToolExecutionResult.Ok(JsonSerializer.Serialize(new
        {
            adjudicationCaseId = adjudicationCase.Id,
            status = adjudicationCase.Status.ToString(),
            approvedBy = adjudicationCase.ApprovedBy,
        }));
    }
}
