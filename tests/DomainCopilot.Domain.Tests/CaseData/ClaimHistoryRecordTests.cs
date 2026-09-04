using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Domain.Tests.CaseData;

public class ClaimHistoryRecordTests
{
    private static ClaimHistoryRecord CreateValid() => ClaimHistoryRecord.Create(
        claimNumber: "CLM-2025-04417",
        policyNumber: "MMIC-PAP-100234",
        dateOfLoss: new DateOnly(2025, 8, 3),
        lossType: ClaimLossType.Collision,
        description: "Insured vehicle struck another vehicle's rear bumper.",
        estimatedDamage: 3_200m,
        policeReportNumber: "DPD-2025-118834",
        isGlassOnly: false,
        flaggedAnomaly: null);

    [Fact]
    public void Create_ValidInput_PopulatesAllFields()
    {
        var record = CreateValid();

        Assert.Equal("CLM-2025-04417", record.ClaimNumber);
        Assert.Equal("MMIC-PAP-100234", record.PolicyNumber);
        Assert.Equal(ClaimLossType.Collision, record.LossType);
        Assert.Equal(3_200m, record.EstimatedDamage);
        Assert.NotEqual(Guid.Empty, record.Id);
    }

    [Fact]
    public void Create_NoPoliceReport_AllowsNull()
    {
        var record = ClaimHistoryRecord.Create(
            "CLM-2025-04511", "MMIC-PAP-101089", new DateOnly(2025, 8, 14), ClaimLossType.Comprehensive,
            "Windshield cracked by road debris.", 480m, policeReportNumber: null, isGlassOnly: true, flaggedAnomaly: null);

        Assert.Null(record.PoliceReportNumber);
        Assert.True(record.IsGlassOnly);
    }

    [Fact]
    public void Create_FlaggedAnomaly_IsPreserved()
    {
        var record = ClaimHistoryRecord.Create(
            "CLM-2025-04999", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), ClaimLossType.Collision,
            "Loss reported before policy inception.", 1_000m, null, false,
            flaggedAnomaly: "Date of loss predates policy effective date.");

        Assert.Equal("Date of loss predates policy effective date.", record.FlaggedAnomaly);
    }

    [Fact]
    public void Create_EmptyClaimNumber_Throws()
    {
        Assert.Throws<ArgumentException>(() => ClaimHistoryRecord.Create(
            "", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), ClaimLossType.Collision, "d", 100m, null, false, null));
    }

    [Fact]
    public void Create_EmptyPolicyNumber_Throws()
    {
        Assert.Throws<ArgumentException>(() => ClaimHistoryRecord.Create(
            "CLM-2025-04417", "", new DateOnly(2025, 8, 3), ClaimLossType.Collision, "d", 100m, null, false, null));
    }

    [Fact]
    public void Create_NegativeEstimatedDamage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClaimHistoryRecord.Create(
            "CLM-2025-04417", "MMIC-PAP-100234", new DateOnly(2025, 8, 3), ClaimLossType.Collision, "d", -1m, null, false, null));
    }
}
