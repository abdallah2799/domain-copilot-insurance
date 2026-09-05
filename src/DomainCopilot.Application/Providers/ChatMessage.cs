namespace DomainCopilot.Application.Providers;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary><see cref="ToolCalls"/> is only meaningful on an <see cref="ChatRole.Assistant"/>
/// message — it's how a multi-turn tool-calling loop represents the model's own prior tool-call
/// requests back to it once the corresponding <see cref="ChatRole.Tool"/> results are appended.
/// Without it, a follow-up call would send tool results with no matching prior tool-call entry,
/// which a standards-compliant chat API can reject outright.</summary>
public sealed record ChatMessage(ChatRole Role, string Content, string? ToolCallId = null, string? Name = null, IReadOnlyList<ToolCall>? ToolCalls = null)
{
    public static ChatMessage System(string content) => new(ChatRole.System, content);
    public static ChatMessage User(string content) => new(ChatRole.User, content);
    public static ChatMessage Assistant(string content, IReadOnlyList<ToolCall>? toolCalls = null) => new(ChatRole.Assistant, content, ToolCalls: toolCalls);
    public static ChatMessage ToolResult(string toolCallId, string content) => new(ChatRole.Tool, content, toolCallId);
}
