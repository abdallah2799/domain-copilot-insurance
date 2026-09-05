namespace DomainCopilot.EvaluationHarness;

/// <summary>One golden-set entry (docs/evaluation/golden-set.json). A black-box harness model,
/// deliberately not shared with the API's own DTOs -- this tool exercises the system the same way
/// an external client would, over real HTTP, not via an in-process shortcut.</summary>
public sealed record GoldenEntry(
    string Id,
    string Category,
    string Question,
    bool ExpectRefusal,
    string[] ExpectedCitationKeywords,
    string? DateOfLoss = null,
    string? InjectionMarker = null,
    string? Notes = null);
