using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Tests.Retrieval;

internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public string ProviderName => "fake";

    public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>([.. texts.Select(_ => new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f]))]);
}
