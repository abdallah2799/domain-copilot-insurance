namespace DomainCopilot.EvaluationHarness;

// Mirrors the real API's DTOs (camelCase) -- just enough shape for this black-box harness to read
// what it needs back.

public sealed record AskRequest(string Question, string? DateOfLoss = null);

public sealed record CitedChunk(string DocumentTitle, string SectionTitle, int? PageNumber, string Text);

public sealed record AskResult(bool Refused, string Answer, List<string> Citations, List<CitedChunk> RetrievedChunks);

public sealed record ScannedDocumentResult(string Status, string? CombinedText, double? OverallConfidencePercent);
