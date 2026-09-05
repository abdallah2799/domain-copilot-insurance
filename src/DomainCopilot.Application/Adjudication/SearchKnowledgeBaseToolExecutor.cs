using System.Text.Json;
using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Retrieval;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Exposes <see cref="HybridRetrievalService"/> (FR-2, ADR-0005) as the shared
/// knowledge-base search tool every agent has access to — the mechanism by which an agent grounds a
/// statement in the corpus rather than asserting it from training data.</summary>
public sealed class SearchKnowledgeBaseToolExecutor(HybridRetrievalService retrievalService) : IToolExecutor
{
    private const int DefaultTopK = 5;

    public ToolDefinition Definition { get; } = new(
        "search_knowledge_base",
        "Searches the knowledge corpus (policy wordings, exclusions, endorsements, and reference guides) for text relevant to a query, returning cited chunks with a hasSufficientEvidence signal. Never state a policy provision, exclusion, or procedure without having retrieved it here first — cite what this returns, don't rely on general knowledge about insurance.",
        """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string" },
            "topK": { "type": "integer", "minimum": 1, "description": "Defaults to 5." },
            "formVersion": { "type": "string", "description": "Optional — restrict to a specific policy form version plus version-agnostic material." }
          },
          "required": ["query"]
        }
        """);

    public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        string query;
        int topK;
        string? formVersion;

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            query = ToolArguments.RequireString(root, "query");
            topK = ToolArguments.OptionalInt(root, "topK") ?? DefaultTopK;
            formVersion = root.TryGetProperty("formVersion", out var fv) && fv.ValueKind == JsonValueKind.String ? fv.GetString() : null;
        }
        catch (JsonException ex)
        {
            return ToolExecutionResult.Failed($"Invalid arguments: {ex.Message}");
        }
        catch (ToolArgumentException ex)
        {
            return ToolExecutionResult.Failed(ex.Message);
        }

        var result = await retrievalService.SearchAsync(new RetrievalQuery(query, topK, null, formVersion, null), cancellationToken);

        var payload = new
        {
            hasSufficientEvidence = result.HasSufficientEvidence,
            chunks = result.Chunks.Select(c => new
            {
                documentTitle = c.DocumentTitle,
                sectionTitle = c.SectionTitle,
                text = c.Text,
                pageNumber = c.PageNumber,
                formVersion = c.FormVersion,
            }),
        };

        return ToolExecutionResult.Ok(JsonSerializer.Serialize(payload));
    }
}
