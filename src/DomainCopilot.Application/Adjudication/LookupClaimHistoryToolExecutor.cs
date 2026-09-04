using System.Text.Json;
using DomainCopilot.Application.CaseData;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="IClaimHistoryRepository"/> as the Anomaly Analyst agent's
/// <c>lookup_claim_history</c> tool — the deterministic half of the 90-day duplicate-claims
/// indicator (Claims Adjudication Guidelines, Section 3).</summary>
public sealed class LookupClaimHistoryToolExecutor(IClaimHistoryRepository repository) : IToolExecutor
{
    private const int DefaultWindowDays = 90;

    public ToolDefinition Definition { get; } = new(
        "lookup_claim_history",
        "Looks up other claims on the same policy with a date of loss within a window (default 90 days) of the current claim's date of loss — used to check the duplicate-claims anomaly indicator. Pass the current claim's own claim number as excludeClaimNumber so it isn't counted against itself.",
        """
        {
          "type": "object",
          "properties": {
            "policyNumber": { "type": "string" },
            "referenceDateOfLoss": { "type": "string", "format": "date", "description": "The current claim's date of loss, as the center of the lookup window." },
            "windowDays": { "type": "integer", "minimum": 1, "description": "Defaults to 90 if omitted." },
            "excludeClaimNumber": { "type": "string", "description": "The current claim's own claim number, so it isn't counted against itself." }
          },
          "required": ["policyNumber", "referenceDateOfLoss"]
        }
        """);

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        string policyNumber;
        DateOnly referenceDate;
        int windowDays;
        string? excludeClaimNumber;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            policyNumber = ToolArguments.RequireString(root, "policyNumber");

            var referenceDateString = ToolArguments.RequireString(root, "referenceDateOfLoss");
            if (!DateOnly.TryParse(referenceDateString, out referenceDate))
            {
                return ToolExecutionResult.Failed("Argument 'referenceDateOfLoss' must be a valid date (yyyy-MM-dd).");
            }

            windowDays = ToolArguments.OptionalInt(root, "windowDays") ?? DefaultWindowDays;

            excludeClaimNumber = root.TryGetProperty("excludeClaimNumber", out var excludeValue) && excludeValue.ValueKind == JsonValueKind.String
                ? excludeValue.GetString()
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

        var claims = await repository.FindByPolicyNumberWithinWindowAsync(policyNumber, referenceDate, windowDays, cancellationToken);
        var otherClaims = excludeClaimNumber is null
            ? claims
            : [.. claims.Where(c => c.ClaimNumber != excludeClaimNumber)];

        var result = new
        {
            duplicateClaimsFound = otherClaims.Count,
            claims = otherClaims.Select(c => new
            {
                claimNumber = c.ClaimNumber,
                dateOfLoss = c.DateOfLoss.ToString("yyyy-MM-dd"),
                lossType = c.LossType.ToString(),
                estimatedDamage = c.EstimatedDamage,
            }),
        };

        return ToolExecutionResult.Ok(JsonSerializer.Serialize(result, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
