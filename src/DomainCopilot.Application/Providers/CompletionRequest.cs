namespace DomainCopilot.Application.Providers;

public sealed record CompletionRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ToolDefinition>? Tools = null,
    double Temperature = 0.2,
    int? MaxTokens = null);

public sealed record CompletionResult(
    string? Content,
    IReadOnlyList<ToolCall> ToolCalls,
    TokenUsage Usage,
    string ProviderName,
    string ModelName);

public sealed record CompletionChunk(string? DeltaContent, bool IsFinal, TokenUsage? Usage = null);
