using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DomainCopilot.Application.Ingestion;

namespace DomainCopilot.Infrastructure.Ingestion;

/// <summary>
/// DOCX extraction via OpenXml paragraph styles directly — no heuristics needed, unlike PDF.
/// A paragraph's <c>ParagraphStyleId</c> ("Heading1"/"Heading2"/"Title") unambiguously marks a
/// heading; every other paragraph is body text, and OpenXml gives exact paragraph boundaries
/// (unlike PDF's line-only reading-order text), so each body paragraph is its own chunking unit.
/// </summary>
public sealed class DocxKnowledgeExtractor
{
    public Task<ExtractedDocument> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var document = WordprocessingDocument.Open(content, false);
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("DOCX has no document body.");

        var sections = new List<ExtractedSection>();
        var currentHeading = string.Empty;
        var bodyParagraphs = new List<string>();

        void Flush()
        {
            if (currentHeading.Length == 0 && bodyParagraphs.Count == 0)
            {
                return;
            }

            // DOCX has no inherent page concept without full layout computation — page number is
            // intentionally null; our endorsement templates are short enough (~1 page) that a
            // citation naming the document is sufficient without one.
            sections.Add(new ExtractedSection(currentHeading, 1, string.Join("\n\n", bodyParagraphs), null));
            bodyParagraphs = [];
        }

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)).Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            var isHeading = styleId is not null && (styleId.StartsWith("Heading", StringComparison.Ordinal) || styleId == "Title");

            if (isHeading)
            {
                Flush();
                currentHeading = text;
            }
            else
            {
                bodyParagraphs.Add(text);
            }
        }

        Flush();
        return Task.FromResult(new ExtractedDocument(sections));
    }
}
