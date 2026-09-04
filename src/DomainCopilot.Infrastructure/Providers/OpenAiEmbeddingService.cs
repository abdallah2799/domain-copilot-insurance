#pragma warning disable SKEXP0001, SKEXP0010
// ITextEmbeddingGenerationService/AddOpenAITextEmbeddingGeneration are marked obsolete in favor of
// Microsoft.Extensions.AI's IEmbeddingGenerator<string, Embedding<float>>, but the SK 1.80
// connector's AddOpenAIEmbeddingGenerator extension isn't resolvable in this package layout;
// staying on the still-functional API rather than chasing a moving target. Fast-follow: migrate
// once the newer surface stabilizes.
#pragma warning disable CS0618

using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace DomainCopilot.Infrastructure.Providers;

public sealed class OpenAiEmbeddingService : IEmbeddingService
{
    // Lazy: Semantic Kernel validates the API key the moment the Kernel is built. This is the
    // embeddings fallback leg (ADR-0003 update) — building eagerly would mean an unconfigured
    // OpenAI key crashes DI resolution for IEmbeddingService entirely, even though Ollama (primary)
    // never needed OpenAI at all. Building lazily means "no key configured" only fails if this
    // leg is actually reached.
    private readonly Lazy<ITextEmbeddingGenerationService> _embeddingService;

    public OpenAiEmbeddingService(OpenAiOptions options)
    {
        _embeddingService = new Lazy<ITextEmbeddingGenerationService>(() => Kernel.CreateBuilder()
            .AddOpenAITextEmbeddingGeneration(options.EmbeddingModel, options.ApiKey)
            .Build()
            .GetRequiredService<ITextEmbeddingGenerationService>());
    }

    public string ProviderName => "OpenAI";

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddings = await _embeddingService.Value.GenerateEmbeddingsAsync([.. texts], cancellationToken: cancellationToken);
            return [.. embeddings];
        }
        catch (Exception ex)
        {
            throw new CompletionProviderException(ProviderName, $"Embedding request failed: {ex.Message}", ex);
        }
    }
}
