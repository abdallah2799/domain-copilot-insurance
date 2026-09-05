using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Tests.Retrieval;

/// <summary>A completion fake that actually streams (unlike <c>SequencedFakeCompletionService</c>,
/// which throws on <see cref="StreamCompleteAsync"/> since nothing using it needed streaming
/// before <see cref="DomainCopilot.Application.Retrieval.AskService.AskStreamAsync"/>) -- lets a
/// test assert both the sequence of deltas produced and that cancellation actually stops
/// enumeration partway through, the same cooperative-cancellation contract the real streaming
/// providers honor.</summary>
internal sealed class FakeStreamingCompletionService : ICompletionService
{
    private readonly IReadOnlyList<string> _deltas;
    private readonly TokenUsage? _finalUsage;

    public FakeStreamingCompletionService(IReadOnlyList<string> deltas, TokenUsage? finalUsage = null)
    {
        _deltas = deltas;
        _finalUsage = finalUsage;
    }

    public string ProviderName => "fake-streaming";

    public int DeltasYielded { get; private set; }

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by AskService.AskStreamAsync.");

    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var delta in _deltas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            DeltasYielded++;
            yield return new CompletionChunk(delta, IsFinal: false);
        }

        if (_finalUsage is { } usage)
        {
            yield return new CompletionChunk(DeltaContent: null, IsFinal: true, Usage: usage);
        }
    }
}
