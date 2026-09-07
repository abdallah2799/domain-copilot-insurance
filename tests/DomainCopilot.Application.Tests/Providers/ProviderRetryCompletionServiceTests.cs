using DomainCopilot.Application.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace DomainCopilot.Application.Tests.Providers;

/// <summary>
/// A rate limit is not a provider failure. Failing over on one spends the next leg's scarcer budget
/// on a condition that clears by itself, and once that leg is exhausted the chain lands on a local
/// model slow enough to time the stage out — which is exactly how a run was lost before this
/// existed.
/// </summary>
public class ProviderRetryCompletionServiceTests
{
    private static readonly TimeSpan NoWait = TimeSpan.FromMilliseconds(1);

    private sealed class ScriptedService(params Func<CompletionResult>[] steps) : ICompletionService
    {
        private int _index;

        public int Calls { get; private set; }

        public string ProviderName => "scripted";

        public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(steps[Math.Min(_index++, steps.Length - 1)]());
        }

        public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
            CompletionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            var result = steps[Math.Min(_index++, steps.Length - 1)]();
            yield return new CompletionChunk(result.Content, IsFinal: false);
            yield return new CompletionChunk(null, IsFinal: true, result.Usage);
            await Task.CompletedTask;
        }
    }

    private static CompletionResult Ok(string content) =>
        new(content, [], TokenUsage.Zero, "scripted", "m");

    private static Func<CompletionResult> RateLimited() =>
        () => throw new CompletionProviderException("scripted", "429 rate_limit_exceeded", isRateLimited: true);

    private static Func<CompletionResult> Broken() =>
        () => throw new CompletionProviderException("scripted", "500 server error");

    private static Func<CompletionResult> TransientParseFault() =>
        () => throw new CompletionProviderException("scripted", "ArgumentOutOfRangeException (index)", isTransient: true);

    private static ProviderRetryCompletionService Sut(ScriptedService inner, int maxAttempts = 3) =>
        new(inner, NullLogger<ProviderRetryCompletionService>.Instance, maxAttempts, NoWait);

    [Fact]
    public async Task RateLimited_WaitsAndRetriesTheSameProvider()
    {
        var inner = new ScriptedService(RateLimited(), () => Ok("recovered"));

        var result = await Sut(inner).CompleteAsync(new CompletionRequest([ChatMessage.User("q")]));

        Assert.Equal("recovered", result.Content);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task RateLimited_EveryAttempt_EventuallyPropagatesSoTheChainCanFailOver()
    {
        var inner = new ScriptedService(RateLimited());

        var ex = await Assert.ThrowsAsync<CompletionProviderException>(
            () => Sut(inner).CompleteAsync(new CompletionRequest([ChatMessage.User("q")])));

        Assert.True(ex.IsRateLimited);
        Assert.Equal(3, inner.Calls);
    }

    // A genuine fault should fail over immediately -- waiting 25 seconds to re-ask a broken provider
    // the same question helps nobody.
    [Fact]
    public async Task NonRateLimitFailure_IsNotRetried()
    {
        var inner = new ScriptedService(Broken());

        await Assert.ThrowsAsync<CompletionProviderException>(
            () => Sut(inner).CompleteAsync(new CompletionRequest([ChatMessage.User("q")])));

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Success_PassesStraightThroughWithNoExtraCall()
    {
        var inner = new ScriptedService(() => Ok("first time"));

        var result = await Sut(inner).CompleteAsync(new CompletionRequest([ChatMessage.User("q")]));

        Assert.Equal("first time", result.Content);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Streaming_RateLimitedBeforeAnyContent_IsRetried()
    {
        var inner = new ScriptedService(RateLimited(), () => Ok("streamed"));

        var chunks = new List<string?>();
        await foreach (var chunk in Sut(inner).StreamCompleteAsync(new CompletionRequest([ChatMessage.User("q")])))
        {
            chunks.Add(chunk.DeltaContent);
        }

        Assert.Contains("streamed", chunks);
        Assert.Equal(2, inner.Calls);
    }

    // The SDK throws while parsing a response the provider returned successfully. Failing over on
    // that costs the run its only fast provider over a fault a single retry usually clears.
    [Fact]
    public async Task TransientClientLibraryFault_IsRetriedOnTheSameProvider()
    {
        var inner = new ScriptedService(TransientParseFault(), () => Ok("parsed second time"));

        var result = await Sut(inner).CompleteAsync(new CompletionRequest([ChatMessage.User("q")]));

        Assert.Equal("parsed second time", result.Content);
        Assert.Equal(2, inner.Calls);
    }

    // A daily quota does not refill while we wait, so retrying it just spends two more requests
    // from the budget that is already gone -- OpenRouter's leg opts out for exactly this reason.
    [Fact]
    public async Task RateLimit_WhenTheLegOptsOut_FailsOverImmediately()
    {
        var inner = new ScriptedService(RateLimited());
        var sut = new ProviderRetryCompletionService(
            inner, NullLogger<ProviderRetryCompletionService>.Instance,
            maxAttempts: 3, rateLimitWait: NoWait, transientWait: NoWait, retryRateLimits: false);

        await Assert.ThrowsAsync<CompletionProviderException>(
            () => sut.CompleteAsync(new CompletionRequest([ChatMessage.User("q")])));

        Assert.Equal(1, inner.Calls);
    }

    // ...but that leg must still retry the transient fault, which is the whole reason it is wrapped.
    [Fact]
    public async Task TransientFault_IsStillRetriedEvenWhenRateLimitRetriesAreOff()
    {
        var inner = new ScriptedService(TransientParseFault(), () => Ok("recovered"));
        var sut = new ProviderRetryCompletionService(
            inner, NullLogger<ProviderRetryCompletionService>.Instance,
            maxAttempts: 3, rateLimitWait: NoWait, transientWait: NoWait, retryRateLimits: false);

        var result = await sut.CompleteAsync(new CompletionRequest([ChatMessage.User("q")]));

        Assert.Equal("recovered", result.Content);
        Assert.Equal(2, inner.Calls);
    }
}
