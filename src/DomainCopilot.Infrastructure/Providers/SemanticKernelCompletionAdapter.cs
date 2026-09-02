using System.Runtime.CompilerServices;
using System.Text.Json;
using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ChatMessage = DomainCopilot.Application.Providers.ChatMessage;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>
/// Shared Semantic Kernel plumbing behind both <see cref="OpenAiCompletionService"/> and
/// <see cref="OllamaCompletionService"/> (Ollama is driven through SK's OpenAI connector against
/// its OpenAI-compatible endpoint) — the two providers differ only in how the underlying
/// <see cref="Kernel"/> is built, not in how requests/responses are translated.
/// </summary>
internal sealed class SemanticKernelCompletionAdapter(string providerName, string modelId, Kernel kernel)
{
    private readonly IChatCompletionService _chatService = kernel.GetRequiredService<IChatCompletionService>();

    public async Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var history = BuildHistory(request);
            var settings = BuildSettings(request);

            var results = await _chatService.GetChatMessageContentsAsync(history, settings, kernel, cancellationToken);
            var message = results[0];

            var toolCalls = message.Items
                .OfType<FunctionCallContent>()
                .Select(fc => new ToolCall(fc.Id ?? Guid.NewGuid().ToString(), fc.FunctionName, SerializeArguments(fc.Arguments)))
                .ToList();

            return new CompletionResult(message.Content, toolCalls, ExtractUsage(message.Metadata), providerName, modelId);
        }
        catch (Exception ex) when (ex is not CompletionProviderException)
        {
            throw new CompletionProviderException(providerName, $"Completion request failed: {ex.Message}", ex);
        }
    }

    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var history = BuildHistory(request);
        var settings = BuildSettings(request);

        var enumerator = _chatService
            .GetStreamingChatMessageContentsAsync(history, settings, kernel, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        await using (enumerator.ConfigureAwait(false))
        {
            while (true)
            {
                bool hasNext;
                StreamingChatMessageContent? current = null;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                    {
                        current = enumerator.Current;
                    }
                }
                catch (Exception ex)
                {
                    throw new CompletionProviderException(providerName, $"Streaming request failed: {ex.Message}", ex);
                }

                if (!hasNext)
                {
                    yield break;
                }

                yield return new CompletionChunk(current!.Content, IsFinal: false);
            }
        }
    }

    private static ChatHistory BuildHistory(CompletionRequest request)
    {
        var history = new ChatHistory();
        foreach (var message in request.Messages)
        {
            switch (message.Role)
            {
                case ChatRole.System:
                    history.AddSystemMessage(message.Content);
                    break;
                case ChatRole.User:
                    history.AddUserMessage(message.Content);
                    break;
                case ChatRole.Assistant:
                    history.AddAssistantMessage(message.Content);
                    break;
                case ChatRole.Tool:
                    history.Add(new FunctionResultContent(
                        functionName: message.Name ?? string.Empty,
                        pluginName: string.Empty,
                        callId: message.ToolCallId ?? string.Empty,
                        result: message.Content).ToChatMessage());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request), $"Unsupported chat role: {message.Role}");
            }
        }

        return history;
    }

    private static OpenAIPromptExecutionSettings BuildSettings(CompletionRequest request)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        if (request.Tools is { Count: > 0 } tools)
        {
            var kernelFunctions = tools.Select(KernelToolMapper.ToKernelFunction).ToList();
            settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(kernelFunctions, autoInvoke: false);
        }

        return settings;
    }

    private static string SerializeArguments(KernelArguments? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return "{}";
        }

        var plain = arguments.ToDictionary(kv => kv.Key, kv => kv.Value);
        return JsonSerializer.Serialize(plain);
    }

    private static TokenUsage ExtractUsage(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue("Usage", out var usageObj) || usageObj is null)
        {
            return TokenUsage.Zero;
        }

        // The OpenAI SDK's usage type (OpenAI.Chat.ChatTokenUsage) isn't referenced directly so
        // this adapter doesn't take a hard dependency on its exact shape across SDK versions —
        // read the well-known property names defensively instead.
        var type = usageObj.GetType();
        var input = type.GetProperty("InputTokenCount")?.GetValue(usageObj) as int?;
        var output = type.GetProperty("OutputTokenCount")?.GetValue(usageObj) as int?;

        return input is null && output is null ? TokenUsage.Zero : new TokenUsage(input ?? 0, output ?? 0);
    }
}
