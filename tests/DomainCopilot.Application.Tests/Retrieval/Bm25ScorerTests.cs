using DomainCopilot.Application.Retrieval;

namespace DomainCopilot.Application.Tests.Retrieval;

public class Bm25ScorerTests
{
    private readonly Bm25Scorer _scorer = new();

    [Fact]
    public void Score_DocumentContainingQueryTerm_IsRanked()
    {
        var corpus = new[]
        {
            "the glass deductible waiver applies to windshield repair",
            "rental reimbursement covers a substitute vehicle",
        };

        var results = _scorer.Score(corpus, "glass windshield");

        var top = Assert.Single(results);
        Assert.Equal(0, top.Index);
        Assert.True(top.Score > 0);
    }

    [Fact]
    public void Score_NoMatchingTerms_ReturnsEmpty()
    {
        var corpus = new[] { "rental reimbursement covers a substitute vehicle" };

        var results = _scorer.Score(corpus, "glass windshield");

        Assert.Empty(results);
    }

    [Fact]
    public void Score_EmptyCorpus_ReturnsEmpty()
    {
        var results = _scorer.Score([], "glass windshield");

        Assert.Empty(results);
    }

    [Fact]
    public void Score_EmptyQuery_ReturnsEmpty()
    {
        var results = _scorer.Score(["glass deductible waiver"], "   ");

        Assert.Empty(results);
    }

    [Fact]
    public void Score_RareTermRanksHigherThanCommonTerm()
    {
        // "policy" appears in every document (low IDF); "subrogation" appears in only one (high IDF).
        var corpus = new[]
        {
            "policy terms and policy conditions govern this policy",
            "policy provisions include subrogation rights against a third party",
            "policy renewal follows the policy anniversary date",
        };

        var results = _scorer.Score(corpus, "subrogation");

        var top = Assert.Single(results);
        Assert.Equal(1, top.Index);
    }

    [Fact]
    public void Score_IsCaseInsensitive()
    {
        var corpus = new[] { "Glass Deductible Waiver" };

        var results = _scorer.Score(corpus, "glass");

        Assert.Single(results);
    }

    [Fact]
    public void Score_HigherTermFrequency_ScoresHigherThanLowerFrequency()
    {
        var corpus = new[]
        {
            "subrogation subrogation subrogation recovery process",
            "a brief mention of subrogation in passing",
        };

        var results = _scorer.Score(corpus, "subrogation");

        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[0].Index);
        Assert.True(results[0].Score > results[1].Score);
    }
}
