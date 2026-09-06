using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DomainCopilot.Application.Providers;

/// <summary>One recorded provider exchange: the fingerprint of the request that produced it, a
/// short human-readable preview so a cassette can be diffed and reviewed by eye, and the real
/// <see cref="CompletionResult"/> the provider returned.</summary>
public sealed record CompletionCassetteEntry(
    string Fingerprint,
    string RequestPreview,
    CompletionResult Response);

/// <summary>
/// Canonical fingerprint of a <see cref="CompletionRequest"/>, used to match a replayed request
/// against a recorded one. Deliberately covers everything that can change the model's answer —
/// every message (including prior tool calls and their results), the tool definitions offered,
/// temperature and max-token settings — so a cassette can never silently serve a response that was
/// recorded for a different question.
/// </summary>
public static class CompletionFingerprint
{
    private static readonly JsonSerializerOptions CanonicalJson = new() { WriteIndented = false };

    public static string Compute(CompletionRequest request)
    {
        var canonical = new
        {
            messages = request.Messages.Select(m => new
            {
                role = m.Role.ToString(),
                content = m.Content,
                toolCallId = m.ToolCallId,
                name = m.Name,
                toolCalls = m.ToolCalls?.Select(t => new { t.Name, t.ArgumentsJson }).ToArray(),
            }).ToArray(),
            tools = request.Tools?.Select(t => new { t.Name, t.Description, t.JsonSchemaParameters }).ToArray(),
            temperature = request.Temperature,
            maxTokens = request.MaxTokens,
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical, CanonicalJson));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>The tail of the conversation is what actually distinguishes one exchange from the
    /// next within a single agent's loop, so the preview is taken from the last message rather than
    /// the (identical, very long) system prompt at the front.</summary>
    public static string Preview(CompletionRequest request)
    {
        var last = request.Messages.Count > 0 ? request.Messages[^1] : null;
        if (last is null)
        {
            return "(empty request)";
        }

        var text = last.Content.ReplaceLineEndings(" ").Trim();
        return $"[{last.Role}] {(text.Length <= 160 ? text : text[..160] + "…")}";
    }
}
