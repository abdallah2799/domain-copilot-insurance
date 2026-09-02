using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Tests.Providers;

/// <summary>Configurable double so provider tests never touch a real LLM.</summary>
internal sealed class FakeCompletionService(string providerName) : ICompletionService
{
    public string ProviderName => providerName;

    public bool WasCalled { get; private set; }

    public Func<CompletionResult>? CompleteResult { get; set; }
    public Exception? CompleteThrows { get; set; }

    public IReadOnlyList<CompletionChunk> StreamChunks { get; set; } = [];
    public Exception? StreamThrowsAfterChunks { get; set; }

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        if (CompleteThrows is not null)
        {
            throw CompleteThrows;
        }

        return Task.FromResult(CompleteResult!());
    }

    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        foreach (var chunk in StreamChunks)
        {
            await Task.Yield();
            yield return chunk;
        }

        if (StreamThrowsAfterChunks is not null)
        {
            throw StreamThrowsAfterChunks;
        }
    }
}
