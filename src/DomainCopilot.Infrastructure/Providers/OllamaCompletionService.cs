using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>
/// Local/offline provider leg of the fallback chain. Ollama exposes an OpenAI-compatible endpoint,
/// so this reuses Semantic Kernel's OpenAI connector pointed at the local server instead of a
/// separate SDK — the API key is a placeholder Ollama ignores.
/// </summary>
public sealed class OllamaCompletionService : ICompletionService
{
    // A local model on modest hardware genuinely takes longer per call than a hosted API,
    // especially for a multi-tool agent turn with a long system prompt — the OpenAI SDK's default
    // HttpClient timeout (100s) was measured to be too short for this project's own agent prompts
    // on this machine, and no amount of retrying fixes a timeout that's structurally too short.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10);

    private readonly SemanticKernelCompletionAdapter _adapter;

    public OllamaCompletionService(OllamaOptions options)
    {
        var httpClient = new HttpClient { Timeout = RequestTimeout };
        _adapter = new SemanticKernelCompletionAdapter(ProviderName, options.Model, () => Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(options.Model, options.ChatCompletionEndpoint, apiKey: "ollama", httpClient: httpClient)
            .Build());
    }

    public string ProviderName => "Ollama";

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.StreamCompleteAsync(request, cancellationToken);
}
