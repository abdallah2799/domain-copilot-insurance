namespace DomainCopilot.Application.Ingestion;

/// <summary>
/// One structural unit pulled out of a source file — a heading and the text under it, up to (but
/// not including) the next heading of equal-or-higher level. This is the boundary the chunker
/// (ADR-0004) chunks along; extraction's whole job is finding these boundaries correctly per
/// format, so chunking never has to guess where one clause ends and the next begins.
/// </summary>
public sealed record ExtractedSection(
    string HeadingText,
    int HeadingLevel,
    string BodyText,
    int? PageNumber);

/// <summary>Everything extract produced from one source file, before cleaning/chunking.</summary>
public sealed record ExtractedDocument(IReadOnlyList<ExtractedSection> Sections)
{
    /// <summary>Total extracted character count — logged pre/post cleaning per FR-1, so a cleaning
    /// bug that silently eats content is visible in ingestion status, not discovered later.</summary>
    public int TotalCharacterCount => Sections.Sum(s => s.BodyText.Length);
}
