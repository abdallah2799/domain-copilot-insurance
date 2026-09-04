using System.Text.Json;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Thrown when a tool call is missing a required argument. Deliberately distinct from
/// <see cref="JsonException"/> (malformed JSON) — both are caught and turned into a
/// <see cref="ToolExecutionResult.Failed"/> by the executor, but the distinction matters for
/// diagnosing which failure mode actually occurred.</summary>
public sealed class ToolArgumentException(string message) : Exception(message);

/// <summary>
/// Reads tool-call arguments with explicit required/optional semantics. A plain
/// <c>JsonSerializer.Deserialize</c> into a record with non-nullable <c>decimal</c> properties would
/// silently default a missing field to 0 rather than erroring — for this codebase's core risk
/// (a silently wrong payout figure), a missing "estimatedDamage" producing a confident $0 result is
/// worse than an explicit failure, so required fields are checked for actual presence here.
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
}
