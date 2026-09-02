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
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public OllamaEmbeddingService(OllamaOptions options)
    {
        var client = new OpenAIClient(new ApiKeyCredential("ollama"), new OpenAIClientOptions { Endpoint = options.ChatCompletionEndpoint });

        var kernel = Kernel.CreateBuilder()
            .AddOpenAITextEmbeddingGeneration(options.EmbeddingModel, client)
            .Build();

        _embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
    }

    public string ProviderName => "Ollama";

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync([.. texts], cancellationToken: cancellationToken);
            return [.. embeddings];
        }
        catch (Exception ex)
        {
            throw new CompletionProviderException(ProviderName, $"Embedding request failed: {ex.Message}", ex);
        }
    }
}
