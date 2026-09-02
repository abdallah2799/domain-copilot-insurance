using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Providers;

/// <summary>
/// Wraps a primary and fallback <see cref="ICompletionService"/> behind the same port. Pure
/// composition over the interface — no provider SDK involved — so the fallback chain itself is
/// unit-testable with fakes, and swapping which concrete providers sit behind it is an
/// Infrastructure/DI change only.
/// </summary>
public sealed class FallbackCompletionService(
    ICompletionService primary,
    ICompletionService fallback,
    ILogger<FallbackCompletionService> logger) : ICompletionService
{
    public string ProviderName => $"{primary.ProviderName}->{fallback.ProviderName}";

    public async Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await primary.CompleteAsync(request, cancellationToken);
        }
        catch (CompletionProviderException ex)
        {
            logger.LogWarning(ex, "Primary completion provider {Provider} failed, falling back to {Fallback}", primary.ProviderName, fallback.ProviderName);
            return await fallback.CompleteAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Falls back only if the primary fails before yielding its first chunk. Once any content has
    /// reached the caller, switching providers would re-send the response from the start and
    /// duplicate output on the client — so a mid-stream failure propagates instead of silently
    /// restarting.
    /// </summary>
    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enumerator = primary.StreamCompleteAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var yieldedAny = false;

        try
        {
            while (true)
            {
                CompletionChunk current;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        yield break;
                    }

                    current = enumerator.Current;
                }
                catch (CompletionProviderException ex) when (!yieldedAny)
                {
                    logger.LogWarning(ex, "Primary completion provider {Provider} failed before streaming any content, falling back to {Fallback}", primary.ProviderName, fallback.ProviderName);
                    break;
                }

                yieldedAny = true;
                yield return current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        await foreach (var chunk in fallback.StreamCompleteAsync(request, cancellationToken))
        {
            yield return chunk;
        }
    }
}
