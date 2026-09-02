using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Providers;

/// <summary>Same fallback-chain approach as <see cref="FallbackCompletionService"/>, applied to embeddings.</summary>
public sealed class FallbackEmbeddingService(
    IEmbeddingService primary,
    IEmbeddingService fallback,
    ILogger<FallbackEmbeddingService> logger) : IEmbeddingService
{
    public string ProviderName => $"{primary.ProviderName}->{fallback.ProviderName}";

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        try
        {
            return await primary.EmbedAsync(texts, cancellationToken);
        }
        catch (CompletionProviderException ex)
        {
            logger.LogWarning(ex, "Primary embedding provider {Provider} failed, falling back to {Fallback}", primary.ProviderName, fallback.ProviderName);
            return await fallback.EmbedAsync(texts, cancellationToken);
        }
    }
}
