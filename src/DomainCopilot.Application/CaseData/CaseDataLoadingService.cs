using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Application.CaseData;

/// <summary>
/// Loads structured case data (Declarations facts, claim history) that the adjudication agents'
/// lookup tools depend on. Deliberately separate from <c>KnowledgeIngestionService</c> (ADR-0004):
/// this is a plain relational load of already-structured facts — no extraction, chunking,
/// embedding, or vector indexing, because none of this is ever semantically searched. Idempotent by
/// natural key (policy/claim number) — an already-loaded record is skipped, not updated, since this
/// corpus's case data doesn't change within a run.
/// </summary>
public sealed class CaseDataLoadingService(
    IPolicyDeclarationRepository declarationRepository,
    IClaimHistoryRepository claimHistoryRepository)
{
    public async Task<CaseDataLoadResult> LoadAsync(
        IReadOnlyList<LoadPolicyDeclarationRequest> declarations,
        IReadOnlyList<LoadClaimHistoryRequest> claims,
        CancellationToken cancellationToken = default)
    {
        var declarationsLoaded = 0;
        var declarationsSkipped = 0;

        foreach (var request in declarations)
        {
            if (await declarationRepository.FindByPolicyNumberAsync(request.PolicyNumber, cancellationToken) is not null)
            {
                declarationsSkipped++;
                continue;
            }

            var declaration = PolicyDeclaration.Create(
                request.PolicyNumber,
                request.NamedInsured,
                request.VehicleYear,
                request.VehicleMake,
                request.VehicleModel,
                request.Vin,
                request.FormVersion,
                request.EffectiveDate,
                request.LiabilityBiPerPerson,
                request.LiabilityBiPerAccident,
                request.LiabilityPd,
                request.MedPay,
                request.UmUimPerPerson,
                request.UmUimPerAccident,
                request.HasCollision,
                request.CollisionDeductible,
                request.HasComprehensive,
                request.ComprehensiveDeductible,
                request.RentalReimbursementDaily,
                request.Endorsements);

            await declarationRepository.AddAsync(declaration, cancellationToken);
            declarationsLoaded++;
        }

        await declarationRepository.SaveChangesAsync(cancellationToken);

        var claimsLoaded = 0;
        var claimsSkipped = 0;

        foreach (var request in claims)
        {
            if (await claimHistoryRepository.FindByClaimNumberAsync(request.ClaimNumber, cancellationToken) is not null)
            {
                claimsSkipped++;
                continue;
            }

            var lossType = ParseLossType(request.LossType, request.ClaimNumber);

            var record = ClaimHistoryRecord.Create(
                request.ClaimNumber,
                request.PolicyNumber,
                request.DateOfLoss,
                lossType,
                request.Description,
                request.EstimatedDamage,
                request.PoliceReportNumber,
                request.IsGlassOnly,
                request.FlaggedAnomaly);

            await claimHistoryRepository.AddAsync(record, cancellationToken);
            claimsLoaded++;
        }

        await claimHistoryRepository.SaveChangesAsync(cancellationToken);

        return new CaseDataLoadResult(declarationsLoaded, declarationsSkipped, claimsLoaded, claimsSkipped);
    }

    // Not every source loss-type string is a valid C# enum literal (e.g. "UM/UIM"), so this is an
    // explicit mapping rather than a bare Enum.TryParse — a source value outside this known set
    // fails loudly rather than being silently miscategorized.
    private static ClaimLossType ParseLossType(string lossType, string claimNumber) => lossType switch
    {
        "Collision" => ClaimLossType.Collision,
        "Comprehensive" => ClaimLossType.Comprehensive,
        "Liability" => ClaimLossType.Liability,
        "UM/UIM" => ClaimLossType.UmUim,
        _ => throw new InvalidOperationException(
            $"Claim {claimNumber} has unrecognized loss type '{lossType}' — expected Collision, Comprehensive, Liability, or UM/UIM."),
    };
}
