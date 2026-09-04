using System.Text.Json;
using DomainCopilot.Application.CaseData;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="IPolicyDeclarationRepository"/> as the Coverage Matcher agent's
/// <c>lookup_declarations</c> tool.</summary>
public sealed class LookupDeclarationsToolExecutor(IPolicyDeclarationRepository repository) : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "lookup_declarations",
        "Looks up the Declarations page facts for a policy number: named insured, form version, effective date, coverage parts held, limits, deductibles, and endorsements. The only permitted way to obtain these facts — never assume or infer them from the claim narrative.",
        """
        {
          "type": "object",
          "properties": {
            "policyNumber": { "type": "string", "description": "The policy number to look up, e.g. MMIC-PAP-100234." }
          },
          "required": ["policyNumber"]
        }
        """);

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        string policyNumber;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            policyNumber = ToolArguments.RequireString(doc.RootElement, "policyNumber");
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.Failed($"Invalid arguments: {ex.Message}");
        }
        catch (ToolArgumentException ex)
        {
            return ToolExecutionResult.Failed(ex.Message);
        }

        var declaration = await repository.FindByPolicyNumberAsync(policyNumber, cancellationToken);
        if (declaration is null)
        {
            return ToolExecutionResult.Failed($"No Declarations record found for policy number '{policyNumber}'.");
        }

        var result = new
        {
            policyNumber = declaration.PolicyNumber,
            namedInsured = declaration.NamedInsured,
            formVersion = declaration.FormVersion,
            effectiveDate = declaration.EffectiveDate.ToString("yyyy-MM-dd"),
            liabilityBiPerPerson = declaration.LiabilityBiPerPerson,
            liabilityBiPerAccident = declaration.LiabilityBiPerAccident,
            liabilityPd = declaration.LiabilityPd,
            medPay = declaration.MedPay,
            umUimPerPerson = declaration.UmUimPerPerson,
            umUimPerAccident = declaration.UmUimPerAccident,
            hasCollision = declaration.HasCollision,
            collisionDeductible = declaration.CollisionDeductible,
            hasComprehensive = declaration.HasComprehensive,
            comprehensiveDeductible = declaration.ComprehensiveDeductible,
            rentalReimbursementDaily = declaration.RentalReimbursementDaily,
            endorsements = declaration.Endorsements,
        };

        return ToolExecutionResult.Ok(JsonSerializer.Serialize(result, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
