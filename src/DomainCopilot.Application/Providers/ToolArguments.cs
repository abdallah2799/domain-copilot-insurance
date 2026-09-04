using System.Text.Json;

namespace DomainCopilot.Application.Providers;

/// <summary>Thrown when a tool call is missing a required argument, or an argument has the wrong
/// JSON type. Deliberately distinct from <see cref="JsonException"/> (malformed JSON syntax) —
/// both are caught and turned into a <see cref="ToolExecutionResult.Failed"/> by the executor, but
/// the distinction matters for diagnosing which failure mode actually occurred.</summary>
public sealed class ToolArgumentException(string message) : Exception(message);

/// <summary>
/// Reads tool-call arguments with explicit required/optional semantics. A plain
/// <c>JsonSerializer.Deserialize</c> into a record with non-nullable properties would silently
/// default a missing field (0 for a decimal, null for a string) rather than erroring — for this
/// codebase's core risk (a silently wrong figure, or a lookup silently keyed on an empty string),
/// required fields are checked for actual presence here instead.
/// </summary>
internal static class ToolArguments
{
    public static decimal RequireDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new ToolArgumentException($"Missing required argument '{name}'.");
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new ToolArgumentException($"Argument '{name}' must be a number.");
        }

        return value.GetDecimal();
    }

    public static decimal? OptionalDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new ToolArgumentException($"Argument '{name}' must be a number.");
        }

        return value.GetDecimal();
    }

    public static bool? OptionalBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new ToolArgumentException($"Argument '{name}' must be a boolean.");
        }

        return value.GetBoolean();
    }

    public static string RequireString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new ToolArgumentException($"Missing required argument '{name}'.");
        }

        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ToolArgumentException($"Argument '{name}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    public static int? OptionalInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new ToolArgumentException($"Argument '{name}' must be a number.");
        }

        return value.GetInt32();
    }
}
