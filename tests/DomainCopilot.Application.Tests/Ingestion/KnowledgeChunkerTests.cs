using DomainCopilot.Application.Ingestion;

namespace DomainCopilot.Application.Tests.Ingestion;

public class KnowledgeChunkerTests
{
    private readonly KnowledgeChunker _chunker = new();

    private static string Words(int count) => string.Join(' ', Enumerable.Range(1, count).Select(i => $"w{i}"));

    [Fact]
    public void Chunk_EmptyDocument_ProducesNoChunks()
    {
        var result = _chunker.Chunk(new ExtractedDocument([]));

        Assert.Empty(result);
    }

    [Fact]
    public void Chunk_NormalSizedSection_EmittedStandaloneWithItsOwnTitleAndPage()
    {
        var section = new ExtractedSection("5.4 Glass-Only Deductible Waiver", 3, Words(120), PageNumber: 7);
        var result = _chunker.Chunk(new ExtractedDocument([section]));

        var chunk = Assert.Single(result);
        Assert.Equal("5.4 Glass-Only Deductible Waiver", chunk.SectionTitle);
        Assert.Equal(7, chunk.PageNumber);
        Assert.Equal(0, chunk.ChunkIndex);
    }

    [Fact]
    public void Chunk_TwoConsecutiveSmallSections_AreMergedIntoOneChunk()
    {
        var sections = new[]
        {
            new ExtractedSection("9.1 Cancellation", 3, Words(10), PageNumber: 12),
            new ExtractedSection("9.2 Non-Renewal", 3, Words(10), PageNumber: 12),
        };

        var result = _chunker.Chunk(new ExtractedDocument(sections));

        var chunk = Assert.Single(result);
        Assert.Contains("9.1 Cancellation", chunk.SectionTitle);
        Assert.Contains("9.2 Non-Renewal", chunk.SectionTitle);
    }

    [Fact]
    public void Chunk_ManySmallSections_FlushOnceMinWordsIsReached_NotAllMergedIntoOne()
    {
        // 5 sections of 10 words each: after 4 (40 words) the buffer should flush, leaving the
        // 5th to start a new pending buffer of its own.
        var sections = Enumerable.Range(1, 5)
            .Select(i => new ExtractedSection($"Section {i}", 3, Words(10), PageNumber: 1))
            .ToArray();

        var result = _chunker.Chunk(new ExtractedDocument(sections));

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].ChunkIndex);
        Assert.Equal(1, result[1].ChunkIndex);
    }

    [Fact]
    public void Chunk_SmallSectionFollowedByNormalSection_SmallOneFlushesOnItsOwn()
    {
        var sections = new[]
        {
            new ExtractedSection("Glossary intro", 2, Words(5), PageNumber: 1),
            new ExtractedSection("Normal section", 2, Words(100), PageNumber: 2),
        };

        var result = _chunker.Chunk(new ExtractedDocument(sections));

        Assert.Equal(2, result.Count);
        Assert.Equal("Glossary intro", result[0].SectionTitle);
        Assert.Equal("Normal section", result[1].SectionTitle);
    }

    [Fact]
    public void Chunk_OversizedSection_SplitsAtParagraphBoundaries()
    {
        // Three paragraphs of 300 words each (900 total, well over MaxWords=400) separated by
        // blank lines, as our extractors are expected to produce.
        var body = string.Join("\n\n", [Words(300), Words(300), Words(300)]);
        var section = new ExtractedSection("Claims Adjudication Guidelines", 2, body, PageNumber: 3);

        var result = _chunker.Chunk(new ExtractedDocument([section]));

        Assert.True(result.Count > 1, "an oversized section must split into more than one chunk");
        Assert.All(result, c => Assert.StartsWith("Claims Adjudication Guidelines (part", c.SectionTitle));
        // Every chunk has sequential indices assigned by the outer Chunk() call, not SplitLarge itself.
        Assert.Equal(Enumerable.Range(0, result.Count), result.Select(c => c.ChunkIndex));
    }

    [Fact]
    public void Chunk_OversizedSection_OverlapsAdjacentParts()
    {
        var body = string.Join("\n\n", [Words(300), Words(300), Words(300)]);
        var section = new ExtractedSection("Guidelines", 2, body, PageNumber: 3);

        var result = _chunker.Chunk(new ExtractedDocument([section]));

        Assert.True(result.Count >= 2);
        // The last ~30 words of part 1 should reappear at the start of part 2 (the overlap).
        var part1TailWord = result[0].Text.Split(' ')[^1];
        Assert.Contains(part1TailWord, result[1].Text);
    }

    [Fact]
    public void Chunk_MixOfSizes_PreservesDocumentOrder()
    {
        var sections = new[]
        {
            new ExtractedSection("A", 2, Words(5), PageNumber: 1),   // small
            new ExtractedSection("B", 2, Words(150), PageNumber: 2), // normal
            new ExtractedSection("C", 2, Words(5), PageNumber: 3),   // small
        };

        var result = _chunker.Chunk(new ExtractedDocument(sections));

        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0].SectionTitle);
        Assert.Equal("B", result[1].SectionTitle);
        Assert.Equal("C", result[2].SectionTitle);
        Assert.Equal(Enumerable.Range(0, 3), result.Select(c => c.ChunkIndex));
    }
}
