using System.Text.Json;
using DomainCopilot.Application.Documents;
using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Retrieval;
using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="PolicyVersionResolver"/> (ADR-0005) as the Coverage Matcher agent's
/// tool for Claims Adjudication Guidelines, Step 1 — resolving which Policy Form edition governs a
/// claim for its date of loss.</summary>
public sealed class ResolvePolicyVersionToolExecutor(IDocumentRepository documentRepository) : IToolExecutor
{
    public ToolDefinition Definition { get; } = new(
        "resolve_policy_version",
        "Resolves which Policy Form version governs a claim for a given date of loss: the latest edition effective on or before that date (Claims Adjudication Guidelines, Step 1). The only permitted way to determine this — never infer it from context or assume the current edition applies.",
        """
        {
          "type": "object",
          "properties": {
            "dateOfLoss": { "type": "string", "format": "date" }
          },
          "required": ["dateOfLoss"]
        }
        """);

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        DateOnly dateOfLoss;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var dateString = ToolArguments.RequireString(doc.RootElement, "dateOfLoss");
            if (!DateOnly.TryParse(dateString, out dateOfLoss))
            {
                return ToolExecutionResult.Failed("Argument 'dateOfLoss' must be a valid date (yyyy-MM-dd).");
            }
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.Failed($"Invalid arguments: {ex.Message}");
        }
        catch (ToolArgumentException ex)
        {
            return ToolExecutionResult.Failed(ex.Message);
        }

        var completedDocuments = await documentRepository.ListByStatusAsync(IngestionStatus.Completed, cancellationToken);
        var resolved = PolicyVersionResolver.Resolve(completedDocuments, dateOfLoss);

        if (resolved is null)
        {
            return ToolExecutionResult.Failed($"No Policy Form version is effective on or before {dateOfLoss:yyyy-MM-dd}.");
        }

        // The effective date is returned alongside the version so the agent can copy it through
        // into its own output rather than having to recall or retype a date from memory.
        var effectiveDate = completedDocuments
            .Where(d => d.Category == DocumentCategory.PolicyForm && d.FormVersion == resolved)
            .Select(d => d.EffectiveDate)
            .FirstOrDefault();

        return ToolExecutionResult.Ok(JsonSerializer.Serialize(new
        {
            formVersion = resolved,
            effectiveDate = effectiveDate?.ToString("yyyy-MM-dd"),
        }));
    }
}
