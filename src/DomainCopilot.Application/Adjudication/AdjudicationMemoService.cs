using System.Text.Json;
using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>T6's document-out entry point: load a case, deserialize whichever stage results it
/// actually has (a run can be asked for its memo at any point, not only once fully decided --
/// see <see cref="AdjudicationMemoGenerator"/> for how a partially-completed run is
/// rendered honestly rather than assumed to be finished), and generate the PDF.</summary>
public sealed class AdjudicationMemoService(IAdjudicationCaseRepository caseRepository, IAdjudicationMemoGenerator generator)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<byte[]?> GenerateMemoAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        var adjudicationCase = await caseRepository.FindByIdAsync(caseId, cancellationToken);
        if (adjudicationCase is null)
        {
            return null;
        }

        var data = new AdjudicationMemoData(
            adjudicationCase,
            Deserialize<CoverageMatchResult>(adjudicationCase.CoverageMatchResultJson),
            Deserialize<AnomalyFindings>(adjudicationCase.AnomalyFindingsJson),
            Deserialize<ExclusionAnalysisResult>(adjudicationCase.ExclusionAnalysisResultJson),
            Deserialize<Recommendation>(adjudicationCase.RecommendationJson));

        return generator.Generate(data);
    }

    private static T? Deserialize<T>(string? json) =>
        json is null ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
}
