using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Tests.Adjudication;

/// <summary>Returns a pre-queued sequence of responses, one per call — lets a test script an entire
/// multi-turn tool-calling conversation (tool call → result → tool call → result → final answer)
/// without a real model.</summary>
internal sealed class SequencedFakeCompletionService : ICompletionService
{
    private readonly Queue<Func<CompletionResult>> _responses = new();

    public string ProviderName => "fake";

    public int CallCount { get; private set; }

    public void Enqueue(Func<CompletionResult> response) => _responses.Enqueue(response);

    public void EnqueueThrow(Exception exception) => _responses.Enqueue(() => throw exception);

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No more responses queued on this fake.");
        }

        return Task.FromResult(_responses.Dequeue()());
    }

    public IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by AgentRunner.");
}
