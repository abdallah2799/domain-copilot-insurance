namespace DomainCopilot.Application.Providers;

/// <summary>
/// Describes a tool an agent may call. <paramref name="JsonSchemaParameters"/> is a JSON Schema
/// document (as a string) so it can be validated before execution — never trust arguments blind.
/// </summary>
public sealed record ToolDefinition(string Name, string Description, string JsonSchemaParameters);

public sealed record ToolCall(string Id, string Name, string ArgumentsJson);
