using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Providers;

/// <summary>
/// Waits out a provider's rate limit and retries the same provider, instead of letting a temporary
/// budget exhaustion look like a provider failure.
///
/// Without this, the fallback chain treats "you have used this minute's tokens" identically to
/// "this provider is broken" and immediately fails over — which is the wrong trade twice over. It
/// spends the next leg's scarcer daily budget on a condition that clears in seconds, and when that
/// leg is exhausted too the chain lands on a local model that is an order of magnitude slower,
/// turning a short pause into a stage timeout.
///
/// This is the real shape of the constraint on this project's providers: Groq's free tier allows
/// 1000 requests a day but only 8,000 tokens a minute, and this project's agent prompts (a long
/// system prompt with worked examples, tool schemas, and accumulated tool results) are large enough
/// that two or three calls exhaust a minute's tokens while barely touching the daily request budget.
/// The budget is there; it just needs to be drawn at the rate the provider allows.
///
/// Distinct from <c>AgentRunner</c>'s retry-with-backoff, which sits above the whole chain and
/// exists for transient faults. This one is provider-specific and deliberately waits far longer,
/// because a per-minute token bucket does not refill in the couple of seconds an exponential
/// backoff starts with.
/// </summary>
public sealed class RateLimitAwareCompletionService(
    ICompletionService inner,
    ILogger<RateLimitAwareCompletionService> logger,
    int maxAttempts = 3,
    TimeSpan? defaultWait = null) : ICompletionService
{
    private readonly TimeSpan _defaultWait = defaultWait ?? TimeSpan.FromSeconds(25);

    public string ProviderName => inner.ProviderName;

    public async Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await inner.CompleteAsync(request, cancellationToken);
            }
            catch (CompletionProviderException ex) when (ex.IsRateLimited && attempt < maxAttempts)
            {
                var wait = ex.RetryAfter ?? _defaultWait;
                logger.LogWarning(
                    "{Provider} rate-limited on attempt {Attempt}/{MaxAttempts}; waiting {Wait} before retrying the same provider rather than failing over.",
                    ProviderName, attempt, maxAttempts, wait);
                await Task.Delay(wait, cancellationToken);
            }
        }
    }

    /// <summary>Only a rate limit raised before any content reached the caller can be retried —
    /// once a stream has started, replaying it from the beginning would duplicate output, the same
    /// constraint <see cref="FallbackCompletionService"/> observes for failing over mid-stream.</summary>
    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var enumerator = inner.StreamCompleteAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
            var yieldedAny = false;
            var retry = false;

            try
            {
                while (true)
                {
                    CompletionChunk current;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                        {
                            break;
                        }

                        current = enumerator.Current;
                    }
                    catch (CompletionProviderException ex) when (ex.IsRateLimited && !yieldedAny && attempt < maxAttempts)
                    {
                        var wait = ex.RetryAfter ?? _defaultWait;
                        logger.LogWarning(
                            "{Provider} rate-limited before streaming any content (attempt {Attempt}/{MaxAttempts}); waiting {Wait}.",
                            ProviderName, attempt, maxAttempts, wait);
                        await Task.Delay(wait, cancellationToken);
                        retry = true;
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

            if (!retry)
            {
                yield break;
            }
        }
    }
}
