using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Providers;

/// <summary>
/// Retries the same provider on a condition that clears by itself, instead of letting it look like
/// a provider failure and trigger a fail-over.
///
/// Two such conditions, with very different right answers:
///
/// A <b>rate limit</b> means the budget exists but is being drawn too fast, and needs a long wait —
/// Groq's free tier allows 1000 requests a day but only 8,000 tokens a minute, and this project's
/// agent prompts are large enough that two or three calls exhaust a minute while barely touching
/// the day.
///
/// A <b>transient client-library fault</b> means the provider answered correctly and the SDK threw
/// while parsing it (see the adapter's IsClientLibraryParseFault). Nothing is exhausted, so the
/// retry should be near-immediate; the next response has a different shape and usually parses.
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
/// Either way the alternative is worse than waiting: failing over spends the next leg's scarcer
/// budget, and once that is gone the chain lands on a local model an order of magnitude slower,
/// turning a recoverable pause into a stage timeout.
///
/// Distinct from <c>AgentRunner</c>'s retry-with-backoff, which sits above the whole chain. This one
/// is per-leg and knows which wait a given condition deserves — a per-minute token bucket does not
/// refill in the couple of seconds an exponential backoff starts with, and an SDK parse fault does
/// not need twenty-five seconds.
/// </summary>
public sealed class ProviderRetryCompletionService(
    ICompletionService inner,
    ILogger<ProviderRetryCompletionService> logger,
    int maxAttempts = 3,
    TimeSpan? rateLimitWait = null,
    TimeSpan? transientWait = null,
    bool retryRateLimits = true) : ICompletionService
{
    private readonly TimeSpan _rateLimitWait = rateLimitWait ?? TimeSpan.FromSeconds(25);
    private readonly TimeSpan _transientWait = transientWait ?? TimeSpan.FromSeconds(2);

    public string ProviderName => inner.ProviderName;

    /// <summary>Whether waiting would actually help. A per-minute token bucket refills, so a rate
    /// limit is worth waiting out; a daily quota does not, and retrying it merely spends two more
    /// requests from the very budget that is already exhausted -- so a leg limited that way opts out
    /// via <c>retryRateLimits: false</c> and fails over immediately instead.</summary>
    private bool ShouldRetry(CompletionProviderException ex) =>
        ex.IsTransient || (ex.IsRateLimited && retryRateLimits);

    private TimeSpan WaitFor(CompletionProviderException ex) =>
        ex.IsRateLimited ? ex.RetryAfter ?? _rateLimitWait : _transientWait;

    public async Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await inner.CompleteAsync(request, cancellationToken);
            }
            catch (CompletionProviderException ex) when (ShouldRetry(ex) && attempt < maxAttempts)
            {
                var wait = WaitFor(ex);
                logger.LogWarning(
                    "{Provider} {Condition} on attempt {Attempt}/{MaxAttempts}; waiting {Wait} before retrying the same provider rather than failing over.",
                    ProviderName, ex.IsRateLimited ? "rate-limited" : "hit a transient client-library fault",
                    attempt, maxAttempts, wait);
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
                    catch (CompletionProviderException ex) when (ShouldRetry(ex) && !yieldedAny && attempt < maxAttempts)
                    {
                        var wait = WaitFor(ex);
                        logger.LogWarning(
                            "{Provider} retryable failure before streaming any content (attempt {Attempt}/{MaxAttempts}); waiting {Wait}.",
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
