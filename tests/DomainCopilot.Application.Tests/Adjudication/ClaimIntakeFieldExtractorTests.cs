using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Adjudication;

public class ClaimIntakeFieldExtractorTests
{
    // Mirrors the real template seed-data/generate/build_corpus.py's build_scanned_claim_forms
    // actually generates, including the surrounding boilerplate the extractor must ignore.
    private const string RealisticIntakeFormText = """
        Meridian Mutual Insurance Company
        CLAIM INTAKE FORM

        Claim Number: CLM-2025-04417
        Policy Number: MMIC-PAP-100234
        Named Insured: Jordan Ellis
        Address: 412 Birchwood Ave, Columbus, OH 43215
        Vehicle: 2021 Honda Accord, VIN 1HGCV1F34MA012345
        Date of Loss: 2025-08-03
        Time of Loss: reported as approximately mid-day unless otherwise noted
        Location of Loss: see description below
        Loss Type: Collision

        Description of Loss:
        Rear-ended at a stoplight by another vehicle.

        Police Report Number: CPD-2025-231044
        """;

    [Fact]
    public void Extract_RealisticIntakeForm_RecoversAllFourFields()
    {
        var fields = ClaimIntakeFieldExtractor.Extract(RealisticIntakeFormText);

        Assert.Equal("CLM-2025-04417", fields.ClaimNumber);
        Assert.Equal("MMIC-PAP-100234", fields.PolicyNumber);
        Assert.Equal(new DateOnly(2025, 8, 3), fields.DateOfLoss);
        Assert.Equal("CPD-2025-231044", fields.PoliceReportNumber);
    }

    [Fact]
    public void Extract_PoliceReportNumberIsNA_TreatedAsAbsent()
    {
        var text = "Claim Number: CLM-1\nPolicy Number: POL-1\nDate of Loss: 2025-01-01\nPolice Report Number: N/A";

        var fields = ClaimIntakeFieldExtractor.Extract(text);

        Assert.Null(fields.PoliceReportNumber);
    }

    [Fact]
    public void Extract_MissingFields_ReturnsNullRatherThanThrowing()
    {
        var fields = ClaimIntakeFieldExtractor.Extract("This text has none of the expected labels at all.");

        Assert.Null(fields.ClaimNumber);
        Assert.Null(fields.PolicyNumber);
        Assert.Null(fields.DateOfLoss);
        Assert.Null(fields.PoliceReportNumber);
    }

    [Fact]
    public void Extract_MalformedDate_ReturnsNullDateRatherThanThrowing()
    {
        var fields = ClaimIntakeFieldExtractor.Extract("Claim Number: CLM-1\nDate of Loss: not-a-real-date");

        Assert.Equal("CLM-1", fields.ClaimNumber);
        Assert.Null(fields.DateOfLoss);
    }

    [Fact]
    public void Extract_IsCaseInsensitiveOnLabels_TesseractSometimesMisreadsCase()
    {
        var fields = ClaimIntakeFieldExtractor.Extract("claim number: CLM-2025-99999\npolicy NUMBER: POL-9");

        Assert.Equal("CLM-2025-99999", fields.ClaimNumber);
        Assert.Equal("POL-9", fields.PolicyNumber);
    }
}
