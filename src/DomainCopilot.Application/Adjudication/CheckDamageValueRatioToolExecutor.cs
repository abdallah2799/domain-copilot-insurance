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

            var exceedsThreshold = DamageToValueRatioChecker.ExceedsThreshold(
                ToolArguments.RequireDecimal(root, "estimatedDamage"),
                ToolArguments.RequireDecimal(root, "approximateVehicleValue"));

            return Task.FromResult(ToolExecutionResult.Ok(JsonSerializer.Serialize(new { exceedsThreshold })));
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
