using DomainCopilot.Application.Ingestion;

namespace DomainCopilot.Application.Tests.Ingestion;

public class TextCleanerTests
{
    [Fact]
    public void Clean_CollapsesRunsOfWhitespaceWithinAParagraph()
    {
        var doc = new ExtractedDocument([new ExtractedSection("Heading", 1, "Too   many\t\tspaces   here.", 1)]);

        var result = TextCleaner.Clean(doc);

        Assert.Equal("Too many spaces here.", result.Sections[0].BodyText);
    }

    [Fact]
    public void Clean_PreservesParagraphBreaks()
    {
        var doc = new ExtractedDocument([new ExtractedSection("Heading", 1, "First paragraph.\n\nSecond   paragraph.", 1)]);

        var result = TextCleaner.Clean(doc);

        Assert.Equal("First paragraph.\n\nSecond paragraph.", result.Sections[0].BodyText);
    }

    [Fact]
    public void Clean_TrimsHeadingAndBodyWhitespace()
    {
        var doc = new ExtractedDocument([new ExtractedSection("  Heading  ", 1, "  Body text.  ", 1)]);

        var result = TextCleaner.Clean(doc);

        Assert.Equal("Heading", result.Sections[0].HeadingText);
        Assert.Equal("Body text.", result.Sections[0].BodyText);
    }

    [Fact]
    public void Clean_DropsSectionsThatAreEntirelyEmptyAfterCleaning()
    {
        var doc = new ExtractedDocument([
            new ExtractedSection("", 1, "   \n\n  ", 1),
            new ExtractedSection("Real heading", 1, "Real body.", 2),
        ]);

        var result = TextCleaner.Clean(doc);

        var kept = Assert.Single(result.Sections);
        Assert.Equal("Real heading", kept.HeadingText);
    }

    [Fact]
    public void Clean_KeepsAHeadingOnlySectionWithNoBody()
    {
        // A heading immediately followed by another heading (no body text between them) must
        // survive cleaning — the chunker relies on merging it forward, not on TextCleaner dropping it.
        var doc = new ExtractedDocument([new ExtractedSection("Lonely Heading", 1, "", 1)]);

        var result = TextCleaner.Clean(doc);

        var kept = Assert.Single(result.Sections);
        Assert.Equal("Lonely Heading", kept.HeadingText);
        Assert.Equal("", kept.BodyText);
    }

    [Fact]
    public void Clean_RemovesEmptyParagraphsWithinBody()
    {
        var doc = new ExtractedDocument([new ExtractedSection("Heading", 1, "First.\n\n   \n\nSecond.", 1)]);

        var result = TextCleaner.Clean(doc);

        Assert.Equal("First.\n\nSecond.", result.Sections[0].BodyText);
    }
}
