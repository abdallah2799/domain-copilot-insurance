using System.Text;

namespace DomainCopilot.Application.Ingestion;

/// <summary>
/// Structure-aware chunking (ADR-0004): chunk at section boundaries, merging undersized sections
/// into their neighbor and splitting oversized ones at paragraph boundaries with light overlap.
/// Deliberately a concrete class, not a port behind an interface — ADR-0004 commits to this one
/// technique for this corpus, not a swappable strategy, so there's nothing to abstract yet.
/// </summary>
public sealed class KnowledgeChunker
{
    private const int MinWords = 40;
    private const int MaxWords = 400;
    private const int OverlapWords = 30;

    public IReadOnlyList<KnowledgeChunk> Chunk(ExtractedDocument document)
    {
        var chunks = new List<KnowledgeChunk>();
        var pendingTitles = new List<string>();
        var pendingWords = new List<string>();
        int? pendingPage = null;

        void FlushPending()
        {
            if (pendingWords.Count == 0)
            {
                return;
            }

            chunks.Add(new KnowledgeChunk(chunks.Count, string.Join(" / ", pendingTitles), string.Join(' ', pendingWords), pendingPage));
            pendingTitles.Clear();
            pendingWords.Clear();
            pendingPage = null;
        }

        foreach (var section in document.Sections)
        {
            var bodyWords = Tokenize(section.BodyText);
            var totalWords = bodyWords.Count + Tokenize(section.HeadingText).Count;

            if (totalWords > MaxWords)
            {
                FlushPending();
                foreach (var sub in SplitLarge(section))
                {
                    chunks.Add(sub with { ChunkIndex = chunks.Count });
                }

                continue;
            }

            if (totalWords < MinWords)
            {
                pendingTitles.Add(section.HeadingText);
                pendingWords.AddRange(Tokenize($"{section.HeadingText}: {section.BodyText}"));
                pendingPage ??= section.PageNumber;

                if (pendingWords.Count >= MinWords)
                {
                    FlushPending();
                }

                continue;
            }

            FlushPending();
            chunks.Add(new KnowledgeChunk(chunks.Count, section.HeadingText, section.BodyText.Trim(), section.PageNumber));
        }

        FlushPending();
        return chunks;
    }

    private static IEnumerable<KnowledgeChunk> SplitLarge(ExtractedSection section)
    {
        var paragraphs = section.BodyText
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var current = new List<string>();
        var overlapCarry = new List<string>();
        var partIndex = 0;

        IEnumerable<KnowledgeChunk> FlushPart()
        {
            if (current.Count == 0)
            {
                yield break;
            }

            partIndex++;
            var words = string.Join(' ', overlapCarry.Concat(current));
            yield return new KnowledgeChunk(0, $"{section.HeadingText} (part {partIndex})", words, section.PageNumber);

            overlapCarry = current.Count > OverlapWords ? current.TakeLast(OverlapWords).ToList() : [.. current];
            current = [];
        }

        foreach (var paragraph in paragraphs)
        {
            var paragraphWords = Tokenize(paragraph);

            if (current.Count > 0 && current.Count + paragraphWords.Count > MaxWords)
            {
                foreach (var part in FlushPart())
                {
                    yield return part;
                }
            }

            current.AddRange(paragraphWords);
        }

        foreach (var part in FlushPart())
        {
            yield return part;
        }

        // A single section with no blank-line paragraph breaks at all (rare, but possible for a
        // dense block of prose) still needs to come out as at least one chunk rather than nothing.
        if (partIndex == 0 && Tokenize(section.BodyText).Count > 0)
        {
            yield return new KnowledgeChunk(0, section.HeadingText, section.BodyText.Trim(), section.PageNumber);
        }
    }

    private static List<string> Tokenize(string text) =>
        [.. text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)];
}
