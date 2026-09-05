using DomainCopilot.Application.Adjudication;
using DomainCopilot.Domain.Adjudication;
using DomainCopilot.Infrastructure.Adjudication;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DomainCopilot.Contract.Tests;

/// <summary>
/// Verifies the T6 memo generator's actual rendered content (ADR-0011) by extracting real text
/// back out of the PDF it produces (PdfPig -- already a real dependency of this codebase, used
/// for knowledge-corpus extraction) rather than only checking that generation didn't throw. A
/// QuestPDF-produced PDF has a real text layer, so this is a direct, reliable content check, not
/// an OCR-based approximation the way verifying a *scanned* PDF's content would need to be.
/// </summary>
public class AdjudicationMemoGeneratorTests
{
    private readonly AdjudicationMemoGenerator _generator = new();

    private static string ExtractText(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        return string.Join("\n", document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
    }

    [Fact]
    public void Generate_ProducesAValidPdf()
    {
        var adjudicationCase = AdjudicationCase.Create("CLM-1", "POL-1", new DateOnly(2025, 8, 3));
        var bytes = _generator.Generate(new AdjudicationMemoData(adjudicationCase, null, null, null, null));

        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    [Fact]
    public void Generate_NoStagesCompleted_RendersEveryStageAsNotYetCompleted()
    {
        var adjudicationCase = AdjudicationCase.Create("CLM-2025-99999", "POL-1", new DateOnly(2025, 8, 3));
        var text = ExtractText(_generator.Generate(new AdjudicationMemoData(adjudicationCase, null, null, null, null)));

        Assert.Contains("CLM-2025-99999", text);
        Assert.Equal(4, CountOccurrences(text, "Not yet completed"));
    }

    [Fact]
    public void Generate_FullyProgressedAndApprovedCase_RendersAllSectionsAndTheDecision()
    {
        var adjudicationCase = AdjudicationCase.Create("CLM-2025-11111", "POL-77", new DateOnly(2025, 8, 3));
        adjudicationCase.BeginCoverageMatching();

        var coverageMatch = new CoverageMatchResult(
            "PAP-2025-STD", new DateOnly(2025, 6, 1), "Collision", true, null, 500m, false,
            ["Roadside Assistance Endorsement (END-RA-01)"], ["Policy Wording PAP-2025-STD, Section 4.1"], null);
        adjudicationCase.RecordCoverageMatch(System.Text.Json.JsonSerializer.Serialize(coverageMatch, WebJsonOptions));

        var anomalyFindings = new AnomalyFindings(false, false, [], false, false, false, false, "No anomalies found.", ["Anomaly source"]);
        adjudicationCase.RecordAnomalyFindings(System.Text.Json.JsonSerializer.Serialize(anomalyFindings, WebJsonOptions));

        var exclusionAnalysis = new ExclusionAnalysisResult(false, [], false, "No exclusions apply to this claim.", ["Exclusion source"]);
        adjudicationCase.RecordExclusionAnalysis(System.Text.Json.JsonSerializer.Serialize(exclusionAnalysis, WebJsonOptions));

        var recommendation = new Recommendation("Approve", 4500.00m, "calculate_standard_payout", "Straightforward collision claim, approve as filed.", ["Recommendation source"]);
        adjudicationCase.RecordRecommendation(System.Text.Json.JsonSerializer.Serialize(recommendation, WebJsonOptions));

        adjudicationCase.Approve("adjuster@meridianmutual.example");

        var text = ExtractText(_generator.Generate(new AdjudicationMemoData(adjudicationCase, coverageMatch, anomalyFindings, exclusionAnalysis, recommendation)));

        Assert.Contains("CLM-2025-11111", text);
        Assert.Contains("Collision", text);
        Assert.Contains("Roadside Assistance Endorsement (END-RA-01)", text);
        Assert.Contains("No anomalies found.", text);
        Assert.Contains("No exclusions apply to this claim.", text);
        Assert.Contains("Approve", text);
        Assert.Contains("$4,500.00", text);
        Assert.Contains("calculate_standard_payout", text);
        Assert.Contains("adjuster@meridianmutual.example", text);
        Assert.Contains("Policy Wording PAP-2025-STD, Section 4.1", text);
        Assert.DoesNotContain("Not yet completed", text);
    }

    [Fact]
    public void Generate_DegradedFailedCase_RendersTheFailureReason()
    {
        var adjudicationCase = AdjudicationCase.Create("CLM-2025-22222", "POL-1", new DateOnly(2025, 8, 3));
        adjudicationCase.BeginCoverageMatching();
        adjudicationCase.MarkFailed("[DEGRADED — AnomalyAnalyst could not complete (exceeded its step timeout).]");

        var text = ExtractText(_generator.Generate(new AdjudicationMemoData(adjudicationCase, null, null, null, null)));

        Assert.Contains("DEGRADED", text);
        Assert.Contains("AnomalyAnalyst could not complete", text);
    }

    private static int CountOccurrences(string text, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }

    private static readonly System.Text.Json.JsonSerializerOptions WebJsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);
}
