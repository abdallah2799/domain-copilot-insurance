using System.Text.RegularExpressions;

namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// Okapi BM25 keyword scoring (ADR-0005) — the keyword-retrieval leg of hybrid search. A concrete
/// class, not an interface, for the same reason as <c>KnowledgeChunker</c>: this is the one chosen
/// algorithm, not something a provider swap needs to vary. Pure and I/O-free so it's unit-testable
/// in isolation; Infrastructure supplies the corpus text and maps scored indices back to chunk rows.
/// </summary>
public sealed partial class Bm25Scorer
{
    private const double K1 = 1.5;
    private const double B = 0.75;

    /// <summary>Scores every document in <paramref name="corpus"/> against <paramref name="query"/>,
    /// returning only documents with at least one matching term, ranked highest score first. The
    /// returned index refers to the position in <paramref name="corpus"/>, so the caller can map
    /// back to whatever it actually represents (a chunk row, in this codebase's only caller).</summary>
    public IReadOnlyList<(int Index, double Score)> Score(IReadOnlyList<string> corpus, string query)
    {
        var queryTerms = Tokenize(query).Distinct().ToList();
        if (corpus.Count == 0 || queryTerms.Count == 0)
        {
            return [];
        }

        var docTokens = corpus.Select(Tokenize).ToList();
        var docLengths = docTokens.Select(t => t.Count).ToList();
        var avgDocLength = docLengths.Count == 0 ? 0 : docLengths.Average();

        var documentFrequency = new Dictionary<string, int>();
        foreach (var tokens in docTokens)
        {
            foreach (var term in tokens.Distinct())
            {
                documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
            }
        }

        var n = corpus.Count;
        var inverseDocumentFrequency = queryTerms.ToDictionary(
            term => term,
            term => Math.Log(1.0 + (n - documentFrequency.GetValueOrDefault(term) + 0.5) / (documentFrequency.GetValueOrDefault(term) + 0.5)));

        var results = new List<(int Index, double Score)>();
        for (var i = 0; i < corpus.Count; i++)
        {
            var termFrequency = docTokens[i]
                .GroupBy(t => t)
                .ToDictionary(g => g.Key, g => g.Count());

            double score = 0;
            foreach (var term in queryTerms)
            {
                if (!termFrequency.TryGetValue(term, out var tf))
                {
                    continue;
                }

                var idf = inverseDocumentFrequency[term];
                var normalizedLength = docLengths[i] / (avgDocLength == 0 ? 1 : avgDocLength);
                score += idf * (tf * (K1 + 1)) / (tf + K1 * (1 - B + B * normalizedLength));
            }

            if (score > 0)
            {
                results.Add((i, score));
            }
        }

        return [.. results.OrderByDescending(r => r.Score)];
    }

    private static List<string> Tokenize(string text) =>
        [.. TokenPattern().Matches(text.ToLowerInvariant()).Select(m => m.Value).Where(t => t.Length >= 2)];

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex TokenPattern();
}
