using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="TotalLossDeterminer"/> (Total Loss Valuation Methodology, Section 1)
/// as an agent-callable tool.</summary>
public sealed class TotalLossDeterminationToolExecutor : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "determine_total_loss",
        "Determines whether a covered auto is a total loss: repair cost plus salvage value reaching or exceeding Actual Cash Value (ACV), OR repair cost alone exceeding 75% of ACV (Total Loss Valuation Methodology, Section 1). Call this before calculate_standard_payout to confirm which payout formula applies — a total loss uses calculate_total_loss_settlement instead.",
        """
        {
          "type": "object",
          "properties": {
            "repairCost": { "type": "number", "minimum": 0 },
            "salvageValue": { "type": "number", "minimum": 0 },
            "actualCashValue": { "type": "number", "exclusiveMinimum": 0 }
          },
          "required": ["repairCost", "salvageValue", "actualCashValue"]
        }
        """);

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            var isTotalLoss = TotalLossDeterminer.IsTotalLoss(
                ToolArguments.RequireDecimal(root, "repairCost"),
                ToolArguments.RequireDecimal(root, "salvageValue"),
                ToolArguments.RequireDecimal(root, "actualCashValue"));

            return Task.FromResult(ToolExecutionResult.Ok(JsonSerializer.Serialize(new { isTotalLoss }, JsonOptions)));
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
