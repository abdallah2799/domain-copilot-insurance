using System.Text.Json;
using DomainCopilot.Application.CaseData;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

/// <summary>
/// Loads structured case data (Declarations facts, claim history) that the adjudication agents'
/// lookup tools depend on — the "seed/load" command for case data, distinct from
/// <see cref="IngestionController"/>'s knowledge-corpus pipeline (ADR-0004: case data is never
/// chunked, embedded, or searched). Reads the corpus generator's own structured export
/// (<c>case-data.json</c>) rather than re-deriving facts from the generated declarations/claims
/// prose documents.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class CaseDataController(CaseDataLoadingService loadingService, IConfiguration configuration) : ControllerBase
{
    // case-data.json is exported directly from the corpus generator's Python dataclasses
    // (facts.py), so its keys are snake_case ("effective_date", "policy_number") — PropertyNamingPolicy
    // bridges that to these records' PascalCase properties; PropertyNameCaseInsensitive alone only
    // bridges casing, not snake_case vs PascalCase, and silently leaving it off previously caused an
    // ArgumentNullException on DateOnly.Parse(null) rather than a clear "field not found" error.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [HttpPost("load")]
    public async Task<ActionResult<CaseDataLoadResult>> Load([FromQuery] string? corpusPath, CancellationToken cancellationToken)
    {
        var root = corpusPath ?? configuration["Ingestion:CorpusPath"] ?? "seed-data/corpus";
        var caseDataPath = Path.Combine(root, "case-data.json");

        if (!System.IO.File.Exists(caseDataPath))
        {
            return NotFound($"No case-data.json at '{caseDataPath}'. Pass ?corpusPath=... or set Ingestion:CorpusPath.");
        }

        var json = await System.IO.File.ReadAllTextAsync(caseDataPath, cancellationToken);
        var payload = JsonSerializer.Deserialize<CaseDataPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException($"'{caseDataPath}' did not deserialize to the expected shape.");

        var declarations = payload.Policyholders.Select(p => new LoadPolicyDeclarationRequest(
            p.PolicyNumber, p.NamedInsured, p.VehicleYear, p.VehicleMake, p.VehicleModel, p.Vin,
            p.FormVersion, DateOnly.Parse(p.EffectiveDate), p.LiabilityBiPerPerson, p.LiabilityBiPerAccident,
            p.LiabilityPd, p.MedPay, p.UmUimPerPerson, p.UmUimPerAccident, p.HasCollision, p.CollisionDeductible,
            p.HasComprehensive, p.ComprehensiveDeductible, p.RentalReimbursementDaily, p.Endorsements)).ToList();

        var claims = payload.Claims.Select(c => new LoadClaimHistoryRequest(
            c.ClaimNumber, c.PolicyNumber, DateOnly.Parse(c.DateOfLoss), c.LossType, c.Description,
            c.EstimatedDamage, c.PoliceReportNumber, c.IsGlassOnly, c.FlaggedAnomaly)).ToList();

        var result = await loadingService.LoadAsync(declarations, claims, cancellationToken);
        return Ok(result);
    }

    private sealed record CaseDataPayload(List<PolicyholderEntry> Policyholders, List<ClaimEntry> Claims);

    private sealed record PolicyholderEntry(
        string PolicyNumber, string NamedInsured, int VehicleYear, string VehicleMake, string VehicleModel,
        string Vin, string FormVersion, string EffectiveDate, decimal LiabilityBiPerPerson,
        decimal LiabilityBiPerAccident, decimal LiabilityPd, decimal? MedPay, decimal UmUimPerPerson,
        decimal UmUimPerAccident, bool HasCollision, decimal? CollisionDeductible, bool HasComprehensive,
        decimal? ComprehensiveDeductible, decimal? RentalReimbursementDaily, List<string> Endorsements);

    private sealed record ClaimEntry(
        string ClaimNumber, string PolicyNumber, string DateOfLoss, string LossType, string Description,
        decimal EstimatedDamage, string? PoliceReportNumber, bool IsGlassOnly, string? FlaggedAnomaly);
}
