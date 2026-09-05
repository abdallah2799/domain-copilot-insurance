namespace DomainCopilot.Application.Retrieval;

/// <summary>The outcome of a grounded question. When <see cref="Refused"/> is true, the corpus
/// didn't have strong enough evidence (FR-2's refusal signal) and no completion call was made at
/// all — <see cref="Answer"/> is a fixed refusal message, not a model output, so a refusal never
/// costs an LLM call. <see cref="RetrievedChunks"/> is always the full retrieval result (even on a
/// refusal, for transparency into what the closest-available material was); <see cref="Citations"/>
/// is the model's own subset of citation identifiers it actually used, only present when not
/// refused.</summary>
public sealed record AskResult(
    bool Refused,
    string Answer,
    IReadOnlyList<string> Citations,
    IReadOnlyList<CitedChunk> RetrievedChunks);
