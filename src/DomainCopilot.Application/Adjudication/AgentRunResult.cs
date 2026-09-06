namespace DomainCopilot.Application.Adjudication;

/// <summary>One tool call an agent actually executed during its run, with the raw result the tool
/// returned. Recorded so a caller can verify an agent's claims about its own tool use against what
/// the loop really did, rather than trusting the model's self-report — see
/// <see cref="AdjudicationDrafterAgent"/>, where a payout figure is only accepted if a deterministic
/// payout tool genuinely produced it.</summary>
public sealed record ExecutedToolCall(string Name, string ResultJson);

/// <summary>The outcome of one agent's run. A malformed/non-conforming final output, an exhausted
/// tool-call iteration budget, or a completion failure after retries are all <see cref="Failed"/> —
/// never a thrown exception — so the orchestrator has a value to act on (graceful degrade to plain
/// RAG, FR-5) rather than a crash.</summary>
public sealed record AgentRunResult<T>(
    bool Success,
    T? Output,
    int IterationsUsed,
    string? ErrorMessage,
    IReadOnlyList<ExecutedToolCall> ExecutedTools)
{
    public static AgentRunResult<T> Ok(T output, int iterationsUsed, IReadOnlyList<ExecutedToolCall>? executedTools = null) =>
        new(true, output, iterationsUsed, null, executedTools ?? []);

    public static AgentRunResult<T> Failed(string errorMessage) => new(false, default, 0, errorMessage, []);
}
