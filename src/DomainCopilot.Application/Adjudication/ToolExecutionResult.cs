namespace DomainCopilot.Application.Adjudication;

/// <summary>The outcome of executing one tool call. A malformed or domain-invalid argument set is
/// <see cref="Failed"/>, not a thrown exception — an agent's tool call is untrusted input crossing
/// a boundary, and the orchestrator needs a result to feed back to the model, not a crash.</summary>
public sealed record ToolExecutionResult(bool Success, string? ResultJson, string? ErrorMessage)
{
    public static ToolExecutionResult Ok(string resultJson) => new(true, resultJson, null);

    public static ToolExecutionResult Failed(string errorMessage) => new(false, null, errorMessage);
}
