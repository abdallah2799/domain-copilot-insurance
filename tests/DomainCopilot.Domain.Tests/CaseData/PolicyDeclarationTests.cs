using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Domain.Tests.CaseData;

public class PolicyDeclarationTests
{
    private static PolicyDeclaration CreateValid(IReadOnlyList<string>? endorsements = null) => PolicyDeclaration.Create(
        policyNumber: "MMIC-PAP-100234",
        namedInsured: "John A. Whitfield",
        vehicleYear: 2021,
        vehicleMake: "Honda",
        vehicleModel: "Accord",
        vin: "1HGCV1F34MA012345",
        formVersion: "PAP-2024-STD",
        effectiveDate: new DateOnly(2024, 3, 1),
        liabilityBiPerPerson: 100_000m,
        liabilityBiPerAccident: 300_000m,
        liabilityPd: 50_000m,
        medPay: 5_000m,
        umUimPerPerson: 100_000m,
        umUimPerAccident: 300_000m,
        hasCollision: true,
        collisionDeductible: 500m,
        hasComprehensive: true,
        comprehensiveDeductible: 250m,
        rentalReimbursementDaily: 30m,
        endorsements: endorsements ?? ["Roadside Assistance Endorsement (END-RA-01)"]);

    [Fact]
    public void Create_ValidInput_PopulatesAllFields()
    {
        var declaration = CreateValid();

        Assert.Equal("MMIC-PAP-100234", declaration.PolicyNumber);
        Assert.Equal("PAP-2024-STD", declaration.FormVersion);
        Assert.True(declaration.HasCollision);
        Assert.Equal(500m, declaration.CollisionDeductible);
        Assert.NotEqual(Guid.Empty, declaration.Id);
    }

    [Fact]
    public void Create_NoEndorsements_ProducesEmptyListNotNull()
    {
        var declaration = CreateValid(endorsements: []);

        Assert.Empty(declaration.Endorsements);
    }

    [Fact]
    public void Create_PreservesMultipleEndorsements()
    {
        var declaration = CreateValid(endorsements: ["END-RA-01", "END-GAP-01"]);

        Assert.Equal(2, declaration.Endorsements.Count);
    }

    [Fact]
    public void Create_EmptyPolicyNumber_Throws()
    {
        Assert.Throws<ArgumentException>(() => PolicyDeclaration.Create(
            policyNumber: "", namedInsured: "X", vehicleYear: 2020, vehicleMake: "Y", vehicleModel: "Z",
            vin: "V", formVersion: "PAP-2024-STD", effectiveDate: new DateOnly(2024, 1, 1),
            liabilityBiPerPerson: 1, liabilityBiPerAccident: 1, liabilityPd: 1, medPay: null,
            umUimPerPerson: 1, umUimPerAccident: 1, hasCollision: false, collisionDeductible: null,
            hasComprehensive: false, comprehensiveDeductible: null, rentalReimbursementDaily: null,
            endorsements: []));
    }

    [Fact]
    public void Create_EmptyFormVersion_Throws()
    {
        Assert.Throws<ArgumentException>(() => PolicyDeclaration.Create(
            policyNumber: "MMIC-PAP-100234", namedInsured: "X", vehicleYear: 2020, vehicleMake: "Y", vehicleModel: "Z",
            vin: "V", formVersion: "", effectiveDate: new DateOnly(2024, 1, 1),
            liabilityBiPerPerson: 1, liabilityBiPerAccident: 1, liabilityPd: 1, medPay: null,
            umUimPerPerson: 1, umUimPerAccident: 1, hasCollision: false, collisionDeductible: null,
            hasComprehensive: false, comprehensiveDeductible: null, rentalReimbursementDaily: null,
            endorsements: []));
    }

    [Fact]
    public void Create_NoCollisionCoverage_DeductibleIsNull()
    {
        var declaration = PolicyDeclaration.Create(
            policyNumber: "MMIC-PAP-101089", namedInsured: "X", vehicleYear: 2022, vehicleMake: "Ford", vehicleModel: "F-150",
            vin: "V", formVersion: "PAP-2024-STD", effectiveDate: new DateOnly(2024, 1, 1),
            liabilityBiPerPerson: 100_000m, liabilityBiPerAccident: 300_000m, liabilityPd: 50_000m, medPay: null,
            umUimPerPerson: 100_000m, umUimPerAccident: 300_000m, hasCollision: false, collisionDeductible: null,
            hasComprehensive: true, comprehensiveDeductible: 500m, rentalReimbursementDaily: null,
            endorsements: []);

        Assert.False(declaration.HasCollision);
        Assert.Null(declaration.CollisionDeductible);
    }
}
