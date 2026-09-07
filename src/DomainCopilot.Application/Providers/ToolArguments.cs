using System.Globalization;
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

        return ReadDecimal(value, name);
    }

    public static decimal? OptionalDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return ReadDecimal(value, name);
    }

    /// <summary>
    /// Accepts a numeric argument written either as a JSON number (4200) or as a quoted numeric
    /// string ("4200"). Models routinely emit the quoted form — tool-call arguments travel as a JSON
    /// string and a model deciding to quote a number inside it is a formatting choice, not a
    /// different value.
    ///
    /// Rejecting the quoted form was a real, run-ending defect rather than a pedantic nicety: the
    /// Anomaly Analyst passed <c>"4200"</c> to check_damage_value_ratio, got back
    /// <c>{"error":"Argument 'estimatedDamage' must be a number."}</c>, retried the call in exactly
    /// the same shape, and looped until it exhausted its iteration budget and failed the whole run.
    /// That was misdiagnosed twice in ADR-0009 as a model capability limit.
    ///
    /// This is coercion, not silent defaulting — the strictness that actually matters (a missing
    /// argument, a null, or a genuinely non-numeric value) is still an error.
    /// </summary>
    private static decimal ReadDecimal(JsonElement value, string name)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetDecimal();
        }

        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new ToolArgumentException($"Argument '{name}' must be a number.");
    }

    public static bool? OptionalBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return ReadBool(value, name);
    }

    /// <summary>
    /// Accepts a boolean written either as a JSON literal (false) or as a quoted string ("false").
    /// Exactly the same defect as <see cref="ReadDecimal"/> covered for numbers, missed here when
    /// that one was fixed — and it cost a whole run to find: the Adjudication Drafter passed
    /// <c>"glassOnlyDeductibleWaiverApplies": "false"</c> to calculate_standard_payout, was told the
    /// argument must be a boolean, and re-sent the identical call for five straight iterations.
    ///
    /// The lesson is that the strictness has to be relaxed consistently across every reader, not
    /// per-type as each one is caught in production: a model quotes scalars as a formatting habit,
    /// and it does not distinguish between the types it is quoting.
    /// </summary>
    private static bool ReadBool(JsonElement value, string name)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        throw new ToolArgumentException($"Argument '{name}' must be a boolean (true or false).");
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

        return (int)ReadDecimal(value, name);
    }
}
