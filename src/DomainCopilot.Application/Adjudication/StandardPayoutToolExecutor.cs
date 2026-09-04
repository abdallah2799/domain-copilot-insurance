using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="StandardPayoutCalculator"/> (Claims Adjudication Guidelines, Step 4)
/// as an agent-callable tool.</summary>
public sealed class StandardPayoutToolExecutor : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "calculate_standard_payout",
        "Computes a repairable-loss payout as min(estimated_damage, applicable_limit) - applicable_deductible, floored at zero (Claims Adjudication Guidelines, Step 4). This is the only permitted way to produce this figure — never estimate or compute it yourself. Does not apply once the vehicle is a total loss; use determine_total_loss first.",
        """
        {
          "type": "object",
          "properties": {
            "estimatedDamage": { "type": "number", "minimum": 0, "description": "The repair estimate for the covered loss." },
            "applicableLimit": { "type": "number", "minimum": 0, "description": "The coverage limit from the Declarations page for this coverage part." },
            "applicableDeductible": { "type": "number", "minimum": 0, "description": "The deductible from the Declarations page for this coverage part." },
            "glassOnlyDeductibleWaiverApplies": { "type": "boolean", "description": "True if the governing form version's glass-only deductible waiver applies to this claim." }
          },
          "required": ["estimatedDamage", "applicableLimit", "applicableDeductible"]
        }
        """);

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            var payout = StandardPayoutCalculator.Calculate(
                ToolArguments.RequireDecimal(root, "estimatedDamage"),
                ToolArguments.RequireDecimal(root, "applicableLimit"),
                ToolArguments.RequireDecimal(root, "applicableDeductible"),
                ToolArguments.OptionalBool(root, "glassOnlyDeductibleWaiverApplies") ?? false);

            return Task.FromResult(ToolExecutionResult.Ok(JsonSerializer.Serialize(new { payout }, JsonOptions)));
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
