namespace DomainCopilot.Application.Providers;

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

public sealed record ChatMessage(ChatRole Role, string Content, string? ToolCallId = null, string? Name = null)
{
    public static ChatMessage System(string content) => new(ChatRole.System, content);
    public static ChatMessage User(string content) => new(ChatRole.User, content);
    public static ChatMessage Assistant(string content) => new(ChatRole.Assistant, content);
    public static ChatMessage ToolResult(string toolCallId, string content) => new(ChatRole.Tool, content, toolCallId);
}
