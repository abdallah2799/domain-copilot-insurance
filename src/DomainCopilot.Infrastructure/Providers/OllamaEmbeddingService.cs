#pragma warning disable SKEXP0001, SKEXP0010
// See the matching comment in OpenAiEmbeddingService.cs re: staying on the pre-Microsoft.Extensions.AI API.
#pragma warning disable CS0618

using System.ClientModel;
using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using OpenAI;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>Local/offline embedding leg — same OpenAI-compatible-endpoint approach as <see cref="OllamaCompletionService"/>.</summary>
public sealed class OllamaEmbeddingService : IEmbeddingService
{
    // Lazy for consistency with OpenAiEmbeddingService — this leg's key is always a hardcoded
    // placeholder so it can't fail the same way, but deferring construction until first use is
    // still the right default for every provider adapter, not just the ones known to be at risk.
    private readonly Lazy<ITextEmbeddingGenerationService> _embeddingService;

    public OllamaEmbeddingService(OllamaOptions options)
    {
        _embeddingService = new Lazy<ITextEmbeddingGenerationService>(() =>
        {
            var client = new OpenAIClient(new ApiKeyCredential("ollama"), new OpenAIClientOptions { Endpoint = options.ChatCompletionEndpoint });
            return Kernel.CreateBuilder()
                .AddOpenAITextEmbeddingGeneration(options.EmbeddingModel, client)
                .Build()
                .GetRequiredService<ITextEmbeddingGenerationService>();
        });
    }

    public string ProviderName => "Ollama";

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
