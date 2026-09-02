namespace DomainCopilot.Application.Providers;

/// <summary>
/// Port covering embedding generation for a single provider. Kept separate from
/// <see cref="ICompletionService"/> because a deployment may reasonably want a different
/// provider/model for embeddings than for chat completion.
/// </summary>
public interface IEmbeddingService
{
    string ProviderName { get; }

    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
