namespace DomainCopilot.EvaluationHarness;

// Mirrors the real API's DTOs (camelCase) -- just enough shape for this black-box harness to read
// what it needs back.

public sealed record AskRequest(string Question, string? DateOfLoss = null);

public sealed record CitedChunk(string DocumentTitle, string SectionTitle, int? PageNumber, string Text);

public sealed record AskResult(bool Refused, string Answer, List<string> Citations, List<CitedChunk> RetrievedChunks);

public sealed record ScannedDocumentResult(string Status, string? CombinedText, double? OverallConfidencePercent);

// FR-8 added authentication after this harness was first built -- every endpoint below now
// requires a bearer token, so the harness logs in once at startup (EVAL_USERNAME/EVAL_PASSWORD,
// defaulting to the seeded analyst account) and attaches it to every request.
public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResult(string Token, string Username, string Role);
