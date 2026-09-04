using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="GapCoverageCalculator"/> (Total Loss Valuation Methodology, Section
/// 5) as an agent-callable tool.</summary>
public sealed class GapCoverageToolExecutor : IPayoutToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "calculate_gap_coverage",
        "Computes the Loan/Lease Gap Coverage benefit (endorsement END-GAP-01): the amount by which the loan/lease balance exceeds the total loss settlement, capped at the endorsement's own limit (Total Loss Valuation Methodology, Section 5). Only call this after calculate_total_loss_settlement, and only if the policyholder holds this endorsement.",
        """
        {
          "type": "object",
          "properties": {
            "loanOrLeaseBalance": { "type": "number", "minimum": 0 },
            "totalLossSettlement": { "type": "number", "minimum": 0 },
            "endorsementLimit": { "type": "number", "minimum": 0 }
          },
          "required": ["loanOrLeaseBalance", "totalLossSettlement", "endorsementLimit"]
        }
        """);

    public ToolExecutionResult Execute(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            var gapPayout = GapCoverageCalculator.Calculate(
                ToolArguments.RequireDecimal(root, "loanOrLeaseBalance"),
                ToolArguments.RequireDecimal(root, "totalLossSettlement"),
                ToolArguments.RequireDecimal(root, "endorsementLimit"));

            return ToolExecutionResult.Ok(JsonSerializer.Serialize(new { gapPayout }, JsonOptions));
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.Failed($"Invalid arguments: {ex.Message}");
        }
        catch (ToolArgumentException ex)
        {
            return ToolExecutionResult.Failed(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ToolExecutionResult.Failed(ex.Message);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
