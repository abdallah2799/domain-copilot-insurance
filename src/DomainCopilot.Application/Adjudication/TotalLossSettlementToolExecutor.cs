using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="TotalLossSettlementCalculator"/> (Total Loss Valuation Methodology,
/// Section 3) as an agent-callable tool.</summary>
public sealed class TotalLossSettlementToolExecutor : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "calculate_total_loss_settlement",
        "Computes a total-loss settlement as Actual Cash Value minus the applicable deductible, plus documented sales tax and title/transfer fees, less salvage value if the insured retains the vehicle (Total Loss Valuation Methodology, Section 3). Only call this after determine_total_loss confirms the vehicle is a total loss.",
        """
        {
          "type": "object",
          "properties": {
            "actualCashValue": { "type": "number", "minimum": 0 },
            "applicableDeductible": { "type": "number", "minimum": 0 },
            "salesTaxAndFees": { "type": "number", "minimum": 0 },
            "salvageValueIfRetained": { "type": "number", "minimum": 0, "description": "0 if the insured surrenders the vehicle rather than retaining salvage." }
          },
          "required": ["actualCashValue", "applicableDeductible", "salesTaxAndFees"]
        }
        """);

    public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            var settlement = TotalLossSettlementCalculator.Calculate(
                ToolArguments.RequireDecimal(root, "actualCashValue"),
                ToolArguments.RequireDecimal(root, "applicableDeductible"),
                ToolArguments.RequireDecimal(root, "salesTaxAndFees"),
                ToolArguments.OptionalDecimal(root, "salvageValueIfRetained") ?? 0m);

            return Task.FromResult(ToolExecutionResult.Ok(JsonSerializer.Serialize(new { settlement }, JsonOptions)));
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
