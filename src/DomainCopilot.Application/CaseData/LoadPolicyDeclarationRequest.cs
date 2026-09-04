namespace DomainCopilot.Application.CaseData;

public sealed record LoadPolicyDeclarationRequest(
    string PolicyNumber,
    string NamedInsured,
    int VehicleYear,
    string VehicleMake,
    string VehicleModel,
    string Vin,
    string FormVersion,
    DateOnly EffectiveDate,
    decimal LiabilityBiPerPerson,
    decimal LiabilityBiPerAccident,
    decimal LiabilityPd,
    decimal? MedPay,
    decimal UmUimPerPerson,
    decimal UmUimPerAccident,
    bool HasCollision,
    decimal? CollisionDeductible,
    bool HasComprehensive,
    decimal? ComprehensiveDeductible,
    decimal? RentalReimbursementDaily,
    IReadOnlyList<string> Endorsements);
