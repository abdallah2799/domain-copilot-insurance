using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="DamageToValueRatioChecker"/> as the Anomaly Analyst agent's tool for
/// the one objectively-checkable anomaly indicator (Claims Adjudication Guidelines, Section 3).</summary>
public sealed class CheckDamageValueRatioToolExecutor : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "check_damage_value_ratio",
        "Determines whether estimated damage exceeds 60% of the vehicle's approximate market value — one of the anomaly indicators. This is a triage-level check using an approximate value, not the rigorous total-loss valuation. The only permitted way to compute this — never estimate it yourself.",
        """
        {
          "type": "object",
          "properties": {
            "estimatedDamage": { "type": "number", "minimum": 0 },
            "approximateVehicleValue": { "type": "number", "exclusiveMinimum": 0 }
          },
          "required": ["estimatedDamage", "approximateVehicleValue"]
        }
        """);

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            var estimatedDamage = ToolArguments.RequireDecimal(root, "estimatedDamage");
            var approximateVehicleValue = ToolArguments.RequireDecimal(root, "approximateVehicleValue");
            var exceedsThreshold = DamageToValueRatioChecker.ExceedsThreshold(estimatedDamage, approximateVehicleValue);
            var ratioPercent = Math.Round(estimatedDamage / approximateVehicleValue * 100m, 1);

            return Task.FromResult(ToolExecutionResult.Ok(JsonSerializer.Serialize(new
            {
                exceedsThreshold,
                ratioPercent,
                thresholdPercent = 60,
                guidance = "This fully answers the damage-to-value indicator for this claim. Do not call check_damage_value_ratio again. " +
                    "If you have not yet called lookup_claim_history, call it now; otherwise you have every indicator you need — respond with the final JSON answer immediately.",
            })));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolExecutionResult.Failed($"Invalid arguments: {ex.Message}"));
        }
        catch (ToolArgumentException ex)
        {
            return Task.FromResult(ToolExecutionResult.Failed(ex.Message));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Task.FromResult(ToolExecutionResult.Failed(ex.Message));
        }
    }
}
