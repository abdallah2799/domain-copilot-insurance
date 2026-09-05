namespace DomainCopilot.Application.Adjudication;

/// <summary>The outcome of one agent's run. A malformed/non-conforming final output, an exhausted
/// tool-call iteration budget, or a completion failure after retries are all <see cref="Failed"/> —
/// never a thrown exception — so the orchestrator has a value to act on (graceful degrade to plain
/// RAG, FR-5) rather than a crash.</summary>
public sealed record AgentRunResult<T>(bool Success, T? Output, int IterationsUsed, string? ErrorMessage)
{
    public static AgentRunResult<T> Ok(T output, int iterationsUsed) => new(true, output, iterationsUsed, null);

    public static AgentRunResult<T> Failed(string errorMessage) => new(false, default, 0, errorMessage);
}
