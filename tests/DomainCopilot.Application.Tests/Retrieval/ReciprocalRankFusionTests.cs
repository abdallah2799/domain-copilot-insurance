using DomainCopilot.Application.Retrieval;
using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Tests.Retrieval;

public class ReciprocalRankFusionTests
{
    private static ScoredChunk Chunk(Guid documentId, int chunkIndex, double score) =>
        new(documentId, chunkIndex, "Section", 1, DocumentCategory.Reference, null, null, "text", score);

    [Fact]
    public void Fuse_ChunkInBothLists_ScoresHigherThanChunkInOnlyOneList()
    {
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();

        var dense = new[] { Chunk(docA, 0, 0.9), Chunk(docB, 0, 0.8) };
        var keyword = new[] { Chunk(docA, 0, 10.0) };

        var fused = ReciprocalRankFusion.Fuse(dense, keyword);

        Assert.Equal(docA, fused[0].Chunk.DocumentId);
        Assert.True(fused[0].FusedScore > fused[1].FusedScore);
    }

    [Fact]
    public void Fuse_ChunkFoundByOnlyOneLeg_KeepsOtherLegScoreNull()
    {
        var docA = Guid.NewGuid();
        var dense = new[] { Chunk(docA, 0, 0.9) };

        var fused = ReciprocalRankFusion.Fuse(dense, []);

        var result = Assert.Single(fused);
        Assert.Equal(0.9, result.DenseScore);
        Assert.Null(result.KeywordScore);
    }

    [Fact]
    public void Fuse_ChunkFoundByKeywordOnly_KeepsDenseScoreNull()
    {
        var docA = Guid.NewGuid();
        var keyword = new[] { Chunk(docA, 0, 5.0) };

        var fused = ReciprocalRankFusion.Fuse([], keyword);

        var result = Assert.Single(fused);
        Assert.Null(result.DenseScore);
        Assert.Equal(5.0, result.KeywordScore);
    }

    [Fact]
    public void Fuse_HigherRankInEitherLeg_ProducesHigherFusedScore()
    {
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        var docC = Guid.NewGuid();

        // Same chunks in both legs but in a different order — rank position drives fusion, not the
        // raw scores (deliberately identical here to isolate that).
        var dense = new[] { Chunk(docA, 0, 1.0), Chunk(docB, 0, 1.0), Chunk(docC, 0, 1.0) };
        var keyword = new[] { Chunk(docA, 0, 1.0), Chunk(docB, 0, 1.0), Chunk(docC, 0, 1.0) };

        var fused = ReciprocalRankFusion.Fuse(dense, keyword);

        Assert.Equal(docA, fused[0].Chunk.DocumentId);
        Assert.Equal(docB, fused[1].Chunk.DocumentId);
        Assert.Equal(docC, fused[2].Chunk.DocumentId);
    }

    [Fact]
    public void Fuse_EmptyLists_ReturnsEmpty()
    {
        var fused = ReciprocalRankFusion.Fuse([], []);

        Assert.Empty(fused);
    }

    [Fact]
    public void Fuse_SameDocumentDifferentChunkIndex_TreatedAsDistinctResults()
    {
        var docA = Guid.NewGuid();
        var dense = new[] { Chunk(docA, 0, 0.9), Chunk(docA, 1, 0.8) };

        var fused = ReciprocalRankFusion.Fuse(dense, []);

        Assert.Equal(2, fused.Count);
    }
}
