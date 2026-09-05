namespace DomainCopilot.Application.Observability;

/// <summary>One completion call's real cost/usage, as recorded by <see cref="ITokenUsageRecorder"/>
/// -- <see cref="CorrelationId"/> is the request's own trace id (see
/// <c>CorrelationIdMiddleware</c>), so every recorded row traces back to the exact request that
/// caused it, not just an isolated number.</summary>
public sealed record TokenUsageEntry(
    string CorrelationId,
    string AgentName,
    string ProviderName,
    string ModelName,
    int PromptTokens,
    int CompletionTokens);
