using System.Text.Json;
using DomainCopilot.Application.Documents;
using DomainCopilot.Application.Ingestion;
using DomainCopilot.Domain.Documents;
using Microsoft.AspNetCore.Mvc;
using KnowledgeDocumentFormat = DomainCopilot.Domain.Documents.DocumentFormat;

namespace DomainCopilot.Api.Controllers;

/// <summary>
/// The FR-1 ingestion trigger and status-reporting surface, and the "seed/ingest command" the
/// brief's Packaging section asks for — a single documented HTTP call rather than a separate
/// undocumented script. Only walks the knowledge-corpus categories (policy-forms, endorsements,
/// reference); declarations/claims are case data and are never routed through this pipeline
/// (ADR-0004).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class IngestionController(
    KnowledgeIngestionService ingestionService,
    IDocumentRepository documentRepository,
    IConfiguration configuration) : ControllerBase
{
    // The two form versions this corpus actually has (facts.py) — the manifest carries the
    // version string but not its effective date, so this is the one place that mapping lives.
    private static readonly Dictionary<string, DateOnly> KnownEffectiveDates = new()
    {
        ["PAP-2024-STD"] = new DateOnly(2024, 1, 1),
        ["PAP-2025-STD"] = new DateOnly(2025, 6, 1),
    };

    private static readonly Dictionary<string, DocumentCategory> KnowledgeCategories = new()
    {
        ["policy-forms"] = DocumentCategory.PolicyForm,
        ["endorsements"] = DocumentCategory.Endorsement,
        ["reference"] = DocumentCategory.Reference,
    };

    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { PropertyNameCaseInsensitive = true };

    [HttpPost("knowledge-corpus")]
    public async Task<ActionResult<IReadOnlyList<IngestionResult>>> IngestKnowledgeCorpus(
        [FromQuery] string? corpusPath,
        CancellationToken cancellationToken)
    {
        var root = corpusPath ?? configuration["Ingestion:CorpusPath"] ?? "seed-data/corpus";
        var manifestPath = Path.Combine(root, "manifest.json");

        if (!System.IO.File.Exists(manifestPath))
        {
            return NotFound($"No manifest.json at '{manifestPath}'. Pass ?corpusPath=... or set Ingestion:CorpusPath.");
        }

        var manifestJson = await System.IO.File.ReadAllTextAsync(manifestPath, cancellationToken);
        var entries = JsonSerializer.Deserialize<List<ManifestEntry>>(manifestJson, ManifestJsonOptions) ?? [];

        var results = new List<IngestionResult>();
        foreach (var entry in entries)
        {
            if (!KnowledgeCategories.TryGetValue(entry.Category, out var category))
            {
                continue; // case data (declarations/claims) — out of scope for this pipeline, ADR-0004
            }

            KnowledgeDocumentFormat? format = entry.Format switch
            {
                "pdf" => KnowledgeDocumentFormat.Pdf,
                "docx" => KnowledgeDocumentFormat.Docx,
                _ => null,
            };

            if (format is null)
            {
                continue; // e.g. pdf-scanned — shouldn't occur outside claims/, but skip defensively
            }

            var effectiveDate = entry.FormVersion is not null && KnownEffectiveDates.TryGetValue(entry.FormVersion, out var date)
                ? date
                : (DateOnly?)null;

            var content = await System.IO.File.ReadAllBytesAsync(Path.Combine(root, entry.Filename), cancellationToken);
            var request = new IngestKnowledgeDocumentRequest(entry.Id, entry.Title, category, format.Value, entry.Filename, entry.FormVersion, effectiveDate, content);

            results.Add(await ingestionService.IngestAsync(request, cancellationToken));
        }

        return Ok(results);
    }

    [HttpGet("~/api/documents")]
    public async Task<ActionResult<IReadOnlyList<Document>>> ListDocuments(CancellationToken cancellationToken) =>
        Ok(await documentRepository.ListAllAsync(cancellationToken));

    private sealed record ManifestEntry(string Id, string Filename, string Category, string Format, string Title, string? FormVersion);
}
