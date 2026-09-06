using System.Text.RegularExpressions;

namespace DomainCopilot.Application.Adjudication;

public sealed record ExtractedClaimIntakeFields(
    string? ClaimNumber,
    string? PolicyNumber,
    DateOnly? DateOfLoss,
    string? PoliceReportNumber);

/// <summary>
/// Deterministic, not LLM-based: the claim intake form this project's corpus generator produces
/// (<c>seed-data/generate/build_corpus.py</c>'s <c>build_scanned_claim_forms</c>) is a fixed
/// "Label: value" template, so a label-based regex reliably recovers the fields an adjudication
/// run needs without spending an LLM call's latency/cost on something a template already makes
/// mechanical -- the same reasoning CLAUDE.md/ADR-0006 already applies to payout math: use
/// deterministic code wherever one is actually available, not an LLM by default. OCR noise can
/// still corrupt any of these values; this extractor doesn't decide how much to trust a low
/// per-page confidence, only the caller (and, ultimately, the human reviewing the pre-filled form)
/// does.
/// </summary>
public static class ClaimIntakeFieldExtractor
{
    private static readonly Regex ClaimNumberPattern = new(@"Claim\s*Number\s*[:\-]\s*(\S+)", RegexOptions.IgnoreCase);
    private static readonly Regex PolicyNumberPattern = new(@"Policy\s*Number\s*[:\-]\s*(\S+)", RegexOptions.IgnoreCase);
    private static readonly Regex DateOfLossPattern = new(@"Date\s*of\s*Loss\s*[:\-]\s*(\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase);
    private static readonly Regex PoliceReportPattern = new(@"Police\s*Report\s*Number\s*[:\-]\s*(\S+)", RegexOptions.IgnoreCase);

    public static ExtractedClaimIntakeFields Extract(string ocrText)
    {
        var claimNumber = MatchValue(ClaimNumberPattern, ocrText);
        var policyNumber = MatchValue(PolicyNumberPattern, ocrText);
        var dateOfLossText = MatchValue(DateOfLossPattern, ocrText);
        var policeReportNumber = MatchValue(PoliceReportPattern, ocrText);

        var dateOfLoss = dateOfLossText is not null && DateOnly.TryParse(dateOfLossText, out var parsed)
            ? parsed
            : (DateOnly?)null;

        // The form prints "N/A" rather than omitting the line entirely when there's no police
        // report -- treat that as "not present," not as a literal report number.
        if (string.Equals(policeReportNumber, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            policeReportNumber = null;
        }

        return new ExtractedClaimIntakeFields(claimNumber, policyNumber, dateOfLoss, policeReportNumber);
    }

    private static string? MatchValue(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
