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
    private readonly SemanticKernelCompletionAdapter _adapter;

    public OllamaCompletionService(OllamaOptions options)
    {
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(options.Model, options.ChatCompletionEndpoint, apiKey: "ollama")
            .Build();

        _adapter = new SemanticKernelCompletionAdapter(ProviderName, options.Model, kernel);
    }

    public string ProviderName => "Ollama";

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.StreamCompleteAsync(request, cancellationToken);
}
