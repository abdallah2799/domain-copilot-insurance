using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Adjudication;

/// <summary>
/// One deterministic payout tool an agent may call. This is the entire enforcement mechanism behind
/// this project's non-negotiable rule that no payout/limit/deductible figure is ever LLM-computed
/// (see CLAUDE.md, "Architecture boundaries"): the model only ever sees <see cref="Definition"/>'s
/// JSON Schema and must call it to get a figure, and <see cref="Execute"/> is the only code path
/// that actually runs the Domain-layer arithmetic — there is no other way for a number to enter an
/// adjudication recommendation.
/// </summary>
public interface IPayoutToolExecutor
{
    ToolDefinition Definition { get; }

    /// <summary>Parses and validates <paramref name="argumentsJson"/> (the model's tool-call
    /// arguments) and, if valid, runs the corresponding Domain calculator. Never throws for
    /// malformed or out-of-range input — see <see cref="ToolExecutionResult"/>.</summary>
    ToolExecutionResult Execute(string argumentsJson);
}
