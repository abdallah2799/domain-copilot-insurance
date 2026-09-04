namespace DomainCopilot.Application.Providers;

/// <summary>
/// One tool an agent may call. This is the entire enforcement mechanism behind this project's rule
/// that a model never directly produces a payout figure, a policy lookup, or any other fact this
/// system can compute or retrieve deterministically (see CLAUDE.md, "Architecture boundaries"): the
/// model only ever sees <see cref="Definition"/>'s JSON Schema and must call it to get an answer,
/// and <see cref="ExecuteAsync"/> is the only code path that actually runs the real logic —
/// deterministic Domain arithmetic for the payout tools, a repository lookup for the case-data
/// tools, retrieval for the knowledge-base tool. Async throughout, since most real implementations
/// need to reach a database or another service, not just compute in memory.
/// </summary>
public interface IToolExecutor
{
    ToolDefinition Definition { get; }

    /// <summary>Parses and validates <paramref name="argumentsJson"/> (the model's tool-call
    /// arguments) and, if valid, executes the tool. Never throws for malformed or out-of-range
    /// input — see <see cref="ToolExecutionResult"/>.</summary>
    Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, CancellationToken cancellationToken = default);
}
