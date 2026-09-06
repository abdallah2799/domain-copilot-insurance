using System.Text.Json;

namespace DomainCopilot.Application.Adjudication;

/// <summary>
/// Rejects a recommendation whose payout figure the model produced itself rather than obtaining
/// from a deterministic payout tool.
///
/// <c>Recommendation.PayoutToolUsed</c> is written by the model, so on its own it is an assertion,
/// not evidence. That distinction is the whole ballgame for this domain: a wrong payout that names
/// a deterministic tool reads as machine-verified, which is considerably worse than an obviously
/// invented one, because it survives review.
///
/// This is not hypothetical. A real run returned <c>payoutAmount: 2700</c> attributed to
/// <c>calculate_standard_payout</c> without having called that tool once — the Drafter had not been
/// given the claim's estimated damage, so it reproduced the worked example from its own prompt
/// (a different claim, $3,200) and did the subtraction itself.
///
/// Checking the claim against the tool calls <see cref="AgentRunner"/> actually executed, and
/// against the value those calls really returned, is what makes "payout math never runs in the LLM"
/// a structural property rather than an instruction the model is trusted to have followed.
/// </summary>
public static class PayoutAttributionVerifier
{
    /// <summary>The tools that can legitimately produce a payout, and the field each returns it in.</summary>
    private static readonly Dictionary<string, string> PayoutProducingTools = new()
    {
        ["calculate_standard_payout"] = "payout",
        ["calculate_total_loss_settlement"] = "settlement",
        ["calculate_gap_coverage"] = "gapPayout",
    };

    public static AgentRunResult<Recommendation> Verify(AgentRunResult<Recommendation> result)
    {
        if (result is not { Success: true, Output: not null })
        {
            return result;
        }

        var recommendation = result.Output;

        // A denial or a request for more information legitimately carries no payout.
        if (recommendation.PayoutAmount is not { } payout)
        {
            return result;
        }

        if (recommendation.PayoutToolUsed is not { } toolName || !PayoutProducingTools.TryGetValue(toolName, out var resultField))
        {
            return AgentRunResult<Recommendation>.Failed(
                $"AdjudicationDrafter: reported a payout of {payout} attributed to '{recommendation.PayoutToolUsed ?? "(none)"}', " +
                $"which is not one of the deterministic payout tools ({string.Join(", ", PayoutProducingTools.Keys)}).");
        }

        var executions = result.ExecutedTools.Where(t => t.Name == toolName).ToList();
        if (executions.Count == 0)
        {
            return AgentRunResult<Recommendation>.Failed(
                $"AdjudicationDrafter: reported a payout of {payout} as coming from '{toolName}', but never called that tool. " +
                "The figure was produced by the model itself, which is exactly what the deterministic payout tools exist to prevent.");
        }

        if (!executions.Any(e => TryReadDecimal(e.ResultJson, resultField) == payout))
        {
            var returned = string.Join(", ", executions.Select(e => TryReadDecimal(e.ResultJson, resultField)?.ToString() ?? "(no value)"));
            return AgentRunResult<Recommendation>.Failed(
                $"AdjudicationDrafter: reported a payout of {payout} from '{toolName}', but that tool returned {returned}.");
        }

        return result;
    }

    private static decimal? TryReadDecimal(string json, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDecimal()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
