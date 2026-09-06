using System.Globalization;
using DomainCopilot.Application.Adjudication;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DomainCopilot.Infrastructure.Adjudication;

/// <summary>
/// T6's document-out half: the adjudication work product an adjuster reviews, built from the same
/// typed stage records the agents themselves produced (ADR-0011) -- never a re-derived summary,
/// and never a number this document computes itself (any payout figure it prints came from
/// <see cref="Recommendation.PayoutAmount"/>, itself only ever set by a deterministic calculator
/// per ADR-0006).
///
/// A memo can be requested for a case at any point, not only once fully decided -- each section
/// renders "Not yet completed" for a stage that hasn't run rather than assuming the case reached
/// a recommendation, since (per ADR-0009) not every real run does.
/// </summary>
public sealed class AdjudicationMemoGenerator : IAdjudicationMemoGenerator
{
    static AdjudicationMemoGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(AdjudicationMemoData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Adjudication Memo").FontSize(20).Bold().FontColor(Colors.Blue.Darken3);
                            left.Item().Text("Meridian Mutual Insurance Company").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(150).AlignRight().Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    header.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                // The adjuster's question is "what am I being asked to approve, and for how much" --
                // so the recommendation leads, and the four agents' working follows as support.
                // Previously every section carried equal weight and the decision was buried in the
                // middle of the page, which read as a transcript rather than a work product.
                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(c => ComposeSummary(c, data));
                    column.Item().Element(c => ComposeDecisionBanner(c, data.Recommendation));
                    column.Item().Element(c => ComposeCoverageSection(c, data.CoverageMatch));
                    column.Item().Element(c => ComposeAnomalySection(c, data.AnomalyFindings));
                    column.Item().Element(c => ComposeExclusionSection(c, data.ExclusionAnalysis));
                    column.Item().Element(c => ComposeRecommendationSection(c, data.Recommendation));
                    column.Item().Element(c => ComposeDecisionSection(c, data.Case));
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Grey.Darken1));
                    x.Span("Domain Copilot — AI-assisted review; not final until an adjuster approves it.   Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeSummary(IContainer container, AdjudicationMemoData data)
    {
        container.Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
        {
            Field(row, "Claim number", data.Case.ClaimNumber);
            Field(row, "Policy number", data.Case.PolicyNumber);
            Field(row, "Date of loss", data.Case.DateOfLoss.ToString("yyyy-MM-dd"));
            Field(row, "Status", SpaceCamelCase(data.Case.Status.ToString()));
        });
    }

    private static void Field(RowDescriptor row, string label, string value) =>
        row.RelativeItem().Column(cell =>
        {
            cell.Item().Text(label.ToUpperInvariant()).FontSize(7).FontColor(Colors.Grey.Darken2).Bold();
            cell.Item().Text(value).FontSize(10);
        });

    /// <summary>"AwaitingApproval" is an enum name, not something to print at a human. </summary>
    private static string SpaceCamelCase(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");

    /// <summary>The headline an adjuster needs before any of the agents' working: what is being
    /// recommended, and for how much. Colour-coded by outcome so an approval and a denial are not
    /// visually identical documents.</summary>
    private static void ComposeDecisionBanner(IContainer container, Recommendation? recommendation)
    {
        if (recommendation is null)
        {
            return;
        }

        var (background, foreground) = recommendation.RecommendationType switch
        {
            "Approve" => (Colors.Green.Lighten5, Colors.Green.Darken3),
            "PartialApprove" => (Colors.Amber.Lighten5, Colors.Amber.Darken4),
            "Deny" => (Colors.Red.Lighten5, Colors.Red.Darken3),
            _ => (Colors.Blue.Lighten5, Colors.Blue.Darken3),
        };

        container.Background(background).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text("RECOMMENDATION").FontSize(7).Bold().FontColor(foreground);
                left.Item().Text(SpaceCamelCase(recommendation.RecommendationType)).FontSize(16).Bold().FontColor(foreground);
            });

            row.ConstantItem(190).AlignRight().Column(right =>
            {
                if (recommendation.PayoutAmount is { } amount)
                {
                    right.Item().AlignRight().Text("RECOMMENDED PAYOUT").FontSize(7).Bold().FontColor(foreground);
                    right.Item().AlignRight().Text(FormatUsd(amount)).FontSize(16).Bold().FontColor(foreground);
                    if (recommendation.PayoutToolUsed is { } tool)
                    {
                        // Named because the figure's provenance is the point: it came from a
                        // deterministic calculator, verified against that tool's own result.
                        right.Item().AlignRight().Text($"calculated by {tool}").FontSize(7).FontColor(Colors.Grey.Darken2);
                    }
                }
                else
                {
                    right.Item().AlignRight().Text("No payout recommended").FontSize(10).Italic().FontColor(foreground);
                }
            });
        });
    }

    private static void ComposeCoverageSection(IContainer container, CoverageMatchResult? coverageMatch)
    {
        container.Column(column =>
        {
            SectionHeading(column, "1. Coverage Match");

            if (coverageMatch is null)
            {
                column.Item().Text("Not yet completed.").Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    HeaderCell(header, "Coverage part");
                    HeaderCell(header, "Selected");
                    HeaderCell(header, "Limit");
                    HeaderCell(header, "Deductible");
                });

                BodyCell(table, coverageMatch.CoveragePart);
                BodyCell(table, coverageMatch.CoveragePartSelected ? "Yes" : "No");
                BodyCell(table, FormatUsd(coverageMatch.ApplicableLimit));
                BodyCell(table, FormatUsd(coverageMatch.ApplicableDeductible));
            });

            column.Item().PaddingTop(6).Row(row =>
            {
                Field(row, "Governing form version", $"{coverageMatch.FormVersion} (effective {coverageMatch.FormVersionEffectiveDate:yyyy-MM-dd})");
                Field(row, "Endorsements held",
                    coverageMatch.EndorsementsHeld.Count > 0 ? string.Join(", ", coverageMatch.EndorsementsHeld) : "None");
            });

            if (!string.IsNullOrWhiteSpace(coverageMatch.Notes))
            {
                column.Item().Text($"Notes: {coverageMatch.Notes}");
            }

            ComposeCitations(column, coverageMatch.Citations);
        });
    }

    private static void ComposeAnomalySection(IContainer container, AnomalyFindings? anomalyFindings)
    {
        container.Column(column =>
        {
            SectionHeading(column, "2. Anomaly Analysis");

            if (anomalyFindings is null)
            {
                column.Item().Text("Not yet completed.").Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            // Every indicator is listed with its outcome, not just the ones that fired: an adjuster
            // needs to see that a check ran and cleared, which a prose summary alone doesn't show.
            var duplicates = anomalyFindings.DuplicateClaimNumbers.Count > 0
                ? $" ({string.Join(", ", anomalyFindings.DuplicateClaimNumbers)})"
                : string.Empty;

            (string Label, bool Fired, string Detail)[] indicators =
            [
                ("Damage-to-value ratio over 60%", anomalyFindings.DamageToValueRatioExceeds60Percent, string.Empty),
                ("Duplicate claims within 90 days", anomalyFindings.DuplicateClaimsWithin90Days, duplicates),
                ("Loss predates policy effective date", anomalyFindings.DateOfLossBeforePolicyEffectiveDate, string.Empty),
                ("Narrative / police report mismatch", anomalyFindings.NarrativePoliceReportMismatch, string.Empty),
                ("Gig-economy use mentioned", anomalyFindings.GigEconomyUseMentioned,
                    anomalyFindings.GigEconomyUseMentioned
                        ? $" (endorsement held: {(anomalyFindings.GigEconomyEndorsementPresent ? "yes" : "no")})"
                        : string.Empty),
            ];

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    HeaderCell(header, "Indicator");
                    HeaderCell(header, "Result");
                });

                foreach (var (label, fired, detail) in indicators)
                {
                    BodyCell(table, label + detail);
                    table.Cell().Padding(4).Text(fired ? "FLAGGED" : "Clear")
                        .FontColor(fired ? Colors.Red.Darken2 : Colors.Green.Darken2)
                        .Bold();
                }
            });

            var firedCount = indicators.Count(i => i.Fired);
            column.Item().PaddingTop(4).Text(firedCount > 0
                    ? $"{firedCount} of {indicators.Length} indicators flagged for adjuster review."
                    : $"All {indicators.Length} indicators clear.")
                .Bold();

            column.Item().PaddingTop(2).Text(anomalyFindings.Summary);
            ComposeCitations(column, anomalyFindings.Citations);
        });
    }

    private static void ComposeExclusionSection(IContainer container, ExclusionAnalysisResult? exclusionAnalysis)
    {
        container.Column(column =>
        {
            SectionHeading(column, "3. Exclusion Analysis");

            if (exclusionAnalysis is null)
            {
                column.Item().Text("Not yet completed.").Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            var (verdict, verdictColor) = exclusionAnalysis.InsufficientInformation
                ? ("Insufficient information to confirm or rule out an exclusion.", Colors.Amber.Darken4)
                : exclusionAnalysis.ExclusionsApply
                    ? ($"Exclusions apply: {string.Join(", ", exclusionAnalysis.ApplicableExclusions)}", Colors.Red.Darken2)
                    : ("No exclusions apply.", Colors.Green.Darken2);

            column.Item().Text(verdict).Bold().FontColor(verdictColor);
            column.Item().PaddingTop(2).Text(exclusionAnalysis.Reasoning);
            ComposeCitations(column, exclusionAnalysis.Citations);
        });
    }

    private static void ComposeRecommendationSection(IContainer container, Recommendation? recommendation)
    {
        container.Column(column =>
        {
            SectionHeading(column, "4. Recommendation");

            if (recommendation is null)
            {
                column.Item().Text("Not yet completed.").Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            // The headline and payout are already at the top of the memo in the decision banner;
            // repeating them here just made the reader scan the same figures twice.
            column.Item().Text(recommendation.Summary);
            ComposeCitations(column, recommendation.Citations);
        });
    }

    private static void ComposeDecisionSection(IContainer container, Domain.Adjudication.AdjudicationCase adjudicationCase)
    {
        if (adjudicationCase.ApprovedBy is null && adjudicationCase.FailureReason is null)
        {
            return;
        }

        container.Column(column =>
        {
            SectionHeading(column, "5. Adjuster Decision");

            if (adjudicationCase.FailureReason is not null)
            {
                column.Item().Background(Colors.Red.Lighten5).Padding(8)
                    .Text(adjudicationCase.FailureReason).FontColor(Colors.Red.Darken2);
            }

            if (adjudicationCase.ApprovedBy is not null)
            {
                column.Item().Text($"{adjudicationCase.Status} by {adjudicationCase.ApprovedBy} at {adjudicationCase.ApprovedAtUtc:yyyy-MM-dd HH:mm} UTC");
                if (!string.IsNullOrWhiteSpace(adjudicationCase.AdjusterComments))
                {
                    column.Item().Text($"Comments: {adjudicationCase.AdjusterComments}");
                }
            }
        });
    }

    private static void SectionHeading(ColumnDescriptor column, string title)
    {
        column.Item().Text(title).Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
        column.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
    }

    private static void ComposeCitations(ColumnDescriptor column, IReadOnlyList<string> citations)
    {
        if (citations.Count == 0)
        {
            return;
        }

        column.Item().PaddingTop(6).Text("Citations").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
        foreach (var citation in citations)
        {
            column.Item().PaddingLeft(10).Text($"• {citation}").FontSize(8).FontColor(Colors.Grey.Darken1);
        }
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Text(label).Bold();
        table.Cell().Text(value);
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).Bold();

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().Padding(4).Text(text);

    // Found via a real CI failure, not assumed: decimal.ToString("C") formats using the current
    // thread's culture, which differed between this dev machine (produced "$4,500.00") and the
    // GitHub Actions runner (produced something else, failing a test asserting on the exact
    // string). This corpus is entirely USD, regardless of which machine renders the memo, so the
    // format is pinned explicitly rather than left to whatever culture happens to be active.
    private static string FormatUsd(decimal? amount) =>
        amount is { } value ? $"${value.ToString("N2", CultureInfo.InvariantCulture)}" : "—";
}
