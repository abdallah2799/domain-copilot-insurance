using System.Text.Json;
using DomainCopilot.Application.Providers;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Adjudication;

/// <summary>
/// The shared tool-calling loop every agent (Coverage Matcher, Anomaly Analyst, Exclusion Analyst,
/// Adjudication Drafter) runs against — FR-4's "each agent... a restricted tool set, defined I/O and
/// a termination condition" is implemented once here, not duplicated per agent: the restricted tool
/// set is whatever <see cref="RunAsync{T}"/> is called with, the typed I/O is the generic parameter
/// <typeparamref name="T"/> the model's final turn must deserialize into, and the termination
/// condition is "the model's turn contains no further tool calls" (a normal end) or the max-iteration
/// breaker (an abnormal one, surfaced as <see cref="AgentRunResult{T}.Failed"/> for the orchestrator
/// to act on).
///
/// Mandatory orchestration controls (FR-5) implemented here: the max-iteration breaker (the tool-call
/// round-trip loop itself), retry-with-backoff (each completion call, independent of the
/// provider-level fallback chain already inside <see cref="ICompletionService"/>). Per-step timeout
/// is the caller's responsibility via <see cref="CancellationToken"/> — every async call here respects
/// it, so the orchestrator can bound an entire agent step with one linked token.
/// </summary>
public sealed class AgentRunner(ICompletionService completionService, ILogger<AgentRunner> logger)
{
    private const int MaxCompletionAttempts = 3;

    public async Task<AgentRunResult<T>> RunAsync<T>(
        string agentName,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<IToolExecutor> availableTools,
        int maxIterations,
        CancellationToken cancellationToken = default)
    {
        var toolsByName = availableTools.ToDictionary(t => t.Definition.Name);
        var toolDefinitions = availableTools.Select(t => t.Definition).ToList();
        var messages = new List<ChatMessage> { ChatMessage.System(systemPrompt), ChatMessage.User(userMessage) };
        IReadOnlyList<string> lastRequestedTools = [];

        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            CompletionResult completion;
            try
            {
                completion = await CompleteWithRetryAsync(new CompletionRequest(messages, toolDefinitions), agentName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // A cancellation from the caller's own token (e.g. the orchestrator's per-step
                // timeout) — distinct from a retry-exhausted failure, and not something retrying
                // again would ever fix, so it's reported as what it actually is rather than the
                // generic "failed after N attempts" message below.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "{Agent} completion failed after retries on iteration {Iteration}", agentName, iteration);
                return AgentRunResult<T>.Failed($"{agentName}: completion call failed after {MaxCompletionAttempts} attempts: {ex.Message}");
            }

            if (completion.ToolCalls.Count == 0)
            {
                // A smaller/local model sometimes writes a tool call out as literal JSON text in
                // its content instead of using the model's actual structured function-calling
                // mechanism — observed directly against this project's own Ollama-served model.
                // Recovering it here (rather than only via prompt wording, which doesn't reliably
                // fix this for a smaller model) means the agent can still complete instead of
                // failing on a call it clearly still intended to make.
                var recovered = TryExtractEmbeddedToolCall(completion.Content ?? string.Empty);
                if (recovered is { } recoveredCall)
                {
                    logger.LogWarning(
                        "{Agent} emitted tool call '{Tool}' as text instead of a structured call on iteration {Iteration}; recovering it.",
                        agentName, recoveredCall.Name, iteration);

                    var syntheticCall = new ToolCall($"recovered-{iteration}", recoveredCall.Name, recoveredCall.ArgumentsJson);
                    messages.Add(ChatMessage.Assistant(completion.Content ?? string.Empty, [syntheticCall]));
                    messages.Add(ChatMessage.ToolResult(syntheticCall.Id, await ExecuteToolCallAsync(syntheticCall, toolsByName, agentName, cancellationToken)));
                    continue;
                }

                return ParseFinalOutput<T>(agentName, completion.Content ?? string.Empty, iteration);
            }

            messages.Add(ChatMessage.Assistant(completion.Content ?? string.Empty, completion.ToolCalls));
            lastRequestedTools = [.. completion.ToolCalls.Select(t => t.Name)];
            logger.LogInformation("{Agent} iteration {Iteration}: requested tool(s) {Tools}", agentName, iteration, string.Join(", ", lastRequestedTools));

            foreach (var toolCall in completion.ToolCalls)
            {
                messages.Add(ChatMessage.ToolResult(toolCall.Id, await ExecuteToolCallAsync(toolCall, toolsByName, agentName, cancellationToken)));
            }
        }

        logger.LogWarning(
            "{Agent} exceeded max iterations ({MaxIterations}); last iteration requested tool(s) {Tools}",
            agentName, maxIterations, string.Join(", ", lastRequestedTools));
        return AgentRunResult<T>.Failed($"{agentName}: exceeded max iterations ({maxIterations}) without a final answer.");
    }

    private static async Task<string> ExecuteToolCallAsync(
        ToolCall toolCall, IReadOnlyDictionary<string, IToolExecutor> toolsByName, string agentName, CancellationToken cancellationToken)
    {
        if (!toolsByName.TryGetValue(toolCall.Name, out var executor))
        {
            return JsonSerializer.Serialize(new { error = $"Tool '{toolCall.Name}' is not available to {agentName}." });
        }

        ToolExecutionResult result;
        try
        {
            result = await executor.ExecuteAsync(toolCall.ArgumentsJson, cancellationToken);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Tool '{toolCall.Name}' threw: {ex.Message}" });
        }

        return result.Success ? result.ResultJson! : JsonSerializer.Serialize(new { error = result.ErrorMessage });
    }

    private async Task<CompletionResult> CompleteWithRetryAsync(CompletionRequest request, string agentName, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt < MaxCompletionAttempts; attempt++)
        {
            try
            {
                return await completionService.CompleteAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                logger.LogWarning(ex, "{Agent} completion attempt {Attempt} failed, retrying in {Delay}", agentName, attempt, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // Final attempt — let a failure here propagate to the caller, which logs and converts it.
        return await completionService.CompleteAsync(request, cancellationToken);
    }

    private static AgentRunResult<T> ParseFinalOutput<T>(string agentName, string content, int iterationsUsed)
    {
        var stripped = StripCodeFence(content);
        if (TryDeserialize<T>(stripped) is { } direct)
        {
            return AgentRunResult<T>.Ok(direct, iterationsUsed);
        }

        // The model sometimes reasons in prose before finally emitting the JSON answer at the very
        // end, despite <final_instruction> saying not to — observed directly against this project's
        // own Ollama-served model, with an otherwise-correct answer that direct parsing alone would
        // have discarded. The last balanced JSON object in the content is tried as a fallback before
        // giving up, since the model's actual answer is usually the last thing it writes.
        var fallback = FindBalancedJsonObjects(content).LastOrDefault();
        if (fallback is not null && TryDeserialize<T>(fallback) is { } recovered)
        {
            return AgentRunResult<T>.Ok(recovered, iterationsUsed);
        }

        return AgentRunResult<T>.Failed($"{agentName} produced non-conforming JSON output. Raw content: {content}");
    }

    private static T? TryDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Scans for a JSON object of the shape <c>{"name": "...", "parameters": {...}}</c>
    /// (or <c>"arguments"</c> instead of <c>"parameters"</c>) anywhere in the content — the shape a
    /// smaller model tends to fall back to when it means to call a tool but doesn't emit a proper
    /// structured tool call. A heuristic, not a guarantee: a genuine final JSON answer that happens
    /// to contain a top-level "name" field would be misread as a tool call, but none of this
    /// project's four output schemas have one.</summary>
    private static (string Name, string ArgumentsJson)? TryExtractEmbeddedToolCall(string content)
    {
        foreach (var candidate in FindBalancedJsonObjects(content))
        {
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("name", out var nameElement)
                    || nameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (root.TryGetProperty("parameters", out var argsElement) || root.TryGetProperty("arguments", out argsElement))
                {
                    return (nameElement.GetString()!, argsElement.GetRawText());
                }
            }
            catch (JsonException)
            {
                // Not a valid JSON object at this position — keep scanning.
            }
        }

        return null;
    }

    private static IEnumerable<string> FindBalancedJsonObjects(string content)
    {
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] != '{')
            {
                continue;
            }

            var candidate = ExtractBalancedJsonObject(content, i);
            if (candidate is not null)
            {
                yield return candidate;
            }
        }
    }

    private static string? ExtractBalancedJsonObject(string content, int startIndex)
    {
        var depth = 0;
        for (var i = startIndex; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                depth++;
            }
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return content[startIndex..(i + 1)];
                }
            }
        }

        return null;
    }

    /// <summary>Local models in particular tend to wrap JSON output in a markdown code fence even
    /// when explicitly told not to — stripped here rather than tightening the prompt further and
    /// hoping, since a parsing failure here is a hard stop for the whole agent step.</summary>
    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
        {
            return trimmed;
        }

        trimmed = trimmed[(firstNewline + 1)..];
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return (lastFence >= 0 ? trimmed[..lastFence] : trimmed).Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
