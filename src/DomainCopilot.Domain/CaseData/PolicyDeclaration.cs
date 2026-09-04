namespace DomainCopilot.Domain.CaseData;

/// <summary>
/// One policyholder's Declarations page facts — coverage parts held, limits, deductibles, and
/// endorsements. Case data, not knowledge-corpus content (ADR-0004): never chunked, embedded, or
/// semantically searched — always looked up by its natural key, <see cref="PolicyNumber"/>, the
/// same way a real claims system reads a policy admin record rather than re-parsing a rendered PDF.
/// Backs the Coverage Matcher agent's <c>lookup_declarations</c> tool.
/// </summary>
public sealed class PolicyDeclaration
{
    public Guid Id { get; private set; }
    public string PolicyNumber { get; private set; } = string.Empty;
    public string NamedInsured { get; private set; } = string.Empty;
    public int VehicleYear { get; private set; }
    public string VehicleMake { get; private set; } = string.Empty;
    public string VehicleModel { get; private set; } = string.Empty;
    public string Vin { get; private set; } = string.Empty;
    public string FormVersion { get; private set; } = string.Empty;
    public DateOnly EffectiveDate { get; private set; }
    public decimal LiabilityBiPerPerson { get; private set; }
    public decimal LiabilityBiPerAccident { get; private set; }
    public decimal LiabilityPd { get; private set; }
    public decimal? MedPay { get; private set; }
    public decimal UmUimPerPerson { get; private set; }
    public decimal UmUimPerAccident { get; private set; }
    public bool HasCollision { get; private set; }
    public decimal? CollisionDeductible { get; private set; }
    public bool HasComprehensive { get; private set; }
    public decimal? ComprehensiveDeductible { get; private set; }
    public decimal? RentalReimbursementDaily { get; private set; }
    public IReadOnlyList<string> Endorsements { get; private set; } = [];

    private PolicyDeclaration()
    {
        // EF Core materialization only — public construction goes through Create.
    }

    public static PolicyDeclaration Create(
        string policyNumber,
        string namedInsured,
        int vehicleYear,
        string vehicleMake,
        string vehicleModel,
        string vin,
        string formVersion,
        DateOnly effectiveDate,
        decimal liabilityBiPerPerson,
        decimal liabilityBiPerAccident,
        decimal liabilityPd,
        decimal? medPay,
        decimal umUimPerPerson,
        decimal umUimPerAccident,
        bool hasCollision,
        decimal? collisionDeductible,
        bool hasComprehensive,
        decimal? comprehensiveDeductible,
        decimal? rentalReimbursementDaily,
        IReadOnlyList<string> endorsements)
    {
        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new ArgumentException("A policy declaration must have a policy number.", nameof(policyNumber));
        }

        if (string.IsNullOrWhiteSpace(namedInsured))
        {
            throw new ArgumentException("A policy declaration must have a named insured.", nameof(namedInsured));
        }

        if (string.IsNullOrWhiteSpace(formVersion))
        {
            throw new ArgumentException("A policy declaration must have a form version.", nameof(formVersion));
        }

        return new PolicyDeclaration
        {
            Id = Guid.NewGuid(),
            PolicyNumber = policyNumber,
            NamedInsured = namedInsured,
            VehicleYear = vehicleYear,
            VehicleMake = vehicleMake,
            VehicleModel = vehicleModel,
            Vin = vin,
            FormVersion = formVersion,
            EffectiveDate = effectiveDate,
            LiabilityBiPerPerson = liabilityBiPerPerson,
            LiabilityBiPerAccident = liabilityBiPerAccident,
            LiabilityPd = liabilityPd,
            MedPay = medPay,
            UmUimPerPerson = umUimPerPerson,
            UmUimPerAccident = umUimPerAccident,
            HasCollision = hasCollision,
            CollisionDeductible = collisionDeductible,
            HasComprehensive = hasComprehensive,
            ComprehensiveDeductible = comprehensiveDeductible,
            RentalReimbursementDaily = rentalReimbursementDaily,
            Endorsements = endorsements,
        };
    }
}
