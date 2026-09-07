using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>
/// Primary hosted completion leg (see ADR-0003). Groq serves open-weight models on its own
/// inference hardware behind an OpenAI-compatible endpoint, so this is the same
/// reuse-the-OpenAI-connector approach as <see cref="OpenRouterCompletionService"/> and
/// <see cref="OllamaCompletionService"/> — a different base URL and key, no new SDK.
///
/// It became the primary leg because OpenRouter's free tier allows 50 model requests per day and a
/// single four-agent adjudication run costs up to ~30 of them: two runs exhausted a day's budget,
/// which made the workflow effectively untestable. Groq's free tier is both far more generous and
/// materially faster per call, which matters for an agent that makes several sequential tool-calling
/// round trips per stage.
///
/// Adding it required this file, an options record, and three lines of DI wiring — no change to any
/// agent, prompt, orchestrator, or Application/Domain type. That is the provider-swap acceptance
/// test the brief asks for, demonstrated rather than asserted.
/// </summary>
public sealed class GroqCompletionService : ICompletionService
{
    private readonly SemanticKernelCompletionAdapter _adapter;

    public GroqCompletionService(GroqOptions options)
    {
        _adapter = new SemanticKernelCompletionAdapter(ProviderName, options.Model, () => Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(options.Model, options.ChatCompletionEndpoint, options.ApiKey)
            .Build());
    }

    public string ProviderName => "Groq";

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.StreamCompleteAsync(request, cancellationToken);
}
