using System.Runtime.CompilerServices;
using System.Text;

namespace DomainCopilot.Application.Providers;

/// <summary>
/// Decorates a real <see cref="ICompletionService"/> and writes every exchange to a cassette on the
/// way through. Pure composition over the port — the wrapped provider does the actual inference, so
/// what lands in the cassette is a genuine provider response, never a hand-written one.
/// </summary>
public sealed class RecordingCompletionService(ICompletionService inner, ICompletionCassetteStore store) : ICompletionService
{
    public string ProviderName => $"recording({inner.ProviderName})";

    public async Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await inner.CompleteAsync(request, cancellationToken);
        await store.AppendAsync(
            new CompletionCassetteEntry(CompletionFingerprint.Compute(request), CompletionFingerprint.Preview(request), result),
            cancellationToken);
        return result;
    }

    /// <summary>Streamed responses are recorded as the single assembled result they add up to:
    /// replay re-chunks the content itself, so how the bytes happened to arrive on the recording run
    /// isn't worth preserving.</summary>
    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var content = new StringBuilder();
        var usage = TokenUsage.Zero;

        await foreach (var chunk in inner.StreamCompleteAsync(request, cancellationToken))
        {
            if (chunk.DeltaContent is not null)
            {
                content.Append(chunk.DeltaContent);
            }

            if (chunk.Usage is not null)
            {
                usage = chunk.Usage;
            }

            yield return chunk;
        }

        var result = new CompletionResult(content.ToString(), [], usage, inner.ProviderName, "streamed");
        await store.AppendAsync(
            new CompletionCassetteEntry(CompletionFingerprint.Compute(request), CompletionFingerprint.Preview(request), result),
            cancellationToken);
    }
}
