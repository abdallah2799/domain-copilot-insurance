using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>
/// Hosted provider leg of the fallback chain (see ADR-0003 update). OpenRouter exposes an
/// OpenAI-compatible endpoint over many underlying models, including genuinely free-tier ones —
/// same reuse-the-OpenAI-connector approach as <see cref="OllamaCompletionService"/>, just against
/// a hosted endpoint instead of a local one.
/// </summary>
public sealed class OpenRouterCompletionService : ICompletionService
{
    private readonly SemanticKernelCompletionAdapter _adapter;

    public OpenRouterCompletionService(OpenRouterOptions options)
    {
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(options.Model, options.ChatCompletionEndpoint, options.ApiKey)
            .Build();

        _adapter = new SemanticKernelCompletionAdapter(ProviderName, options.Model, kernel);
    }

    public string ProviderName => "OpenRouter";

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.StreamCompleteAsync(request, cancellationToken);
}
