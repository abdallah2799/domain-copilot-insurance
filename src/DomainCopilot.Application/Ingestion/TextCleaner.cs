using System.Text.RegularExpressions;

namespace DomainCopilot.Application.Ingestion;

/// <summary>
/// Strips rendering artifacts (extra whitespace, broken line wraps) while preserving paragraph
/// structure — cleaning normalizes presentation, it never drops content the chunker still needs
/// to see (headings, paragraph boundaries).
/// </summary>
public static partial class TextCleaner
{
    public static ExtractedDocument Clean(ExtractedDocument document)
    {
        var cleaned = document.Sections
            .Select(s => s with { HeadingText = CollapseWhitespace(s.HeadingText), BodyText = CleanBody(s.BodyText) })
            .Where(s => s.HeadingText.Length > 0 || s.BodyText.Length > 0)
            .ToList();

        return document with { Sections = cleaned };
    }

    private static string CleanBody(string text)
    {
        var paragraphs = text
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(CollapseWhitespace)
            .Where(p => p.Length > 0);

        return string.Join("\n\n", paragraphs);
    }

    private static string CollapseWhitespace(string text) => WhitespaceRun().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
