namespace DomainCopilot.Application.Retrieval;

public enum AskStreamEventType
{
    /// <summary>Sent once, immediately, when the corpus doesn't have strong enough evidence
    /// (FR-2's refusal signal) — no completion call is made, so a refusal is the only event in the
    /// stream.</summary>
    Refused,

    /// <summary>One per streamed token/delta from the model.</summary>
    Delta,

    /// <summary>Sent once, after the last <see cref="Delta"/>, carrying the citation list. Unlike
    /// <see cref="AskResult"/>'s model-selected citation subset, this is every retrieved chunk's
    /// identifier — the streaming prompt (prompts/ask-stream.md) asks for plain prose with inline
    /// citations rather than a structured JSON object the model would have to hold until the end,
    /// so there's no reliable structured "which citations did you actually use" signal to parse
    /// mid-stream. A deliberate simplification, not an oversight.</summary>
    Done,
}

public sealed record AskStreamEvent(
    AskStreamEventType Type,
    string? DeltaText = null,
    string? RefusalMessage = null,
    IReadOnlyList<CitedChunk>? Chunks = null,
    IReadOnlyList<string>? Citations = null)
{
    public static AskStreamEvent Refused(string message, IReadOnlyList<CitedChunk> chunks) =>
        new(AskStreamEventType.Refused, RefusalMessage: message, Chunks: chunks);

    public static AskStreamEvent Delta(string text) => new(AskStreamEventType.Delta, DeltaText: text);

    public static AskStreamEvent Done(IReadOnlyList<CitedChunk> chunks) =>
        new(AskStreamEventType.Done, Chunks: chunks, Citations: [.. chunks.Select(AskService.CitationId)]);
}
