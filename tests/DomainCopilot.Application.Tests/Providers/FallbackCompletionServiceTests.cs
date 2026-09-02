using DomainCopilot.Application.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace DomainCopilot.Application.Tests.Providers;

public class FallbackCompletionServiceTests
{
    private static readonly CompletionRequest Request = new([ChatMessage.User("hi")]);

    private static FallbackCompletionService Build(FakeCompletionService primary, FakeCompletionService fallback) =>
        new(primary, fallback, NullLogger<FallbackCompletionService>.Instance);

    [Fact]
    public async Task CompleteAsync_WhenPrimarySucceeds_ReturnsPrimaryResultAndNeverCallsFallback()
    {
        var expected = new CompletionResult("primary answer", [], TokenUsage.Zero, "primary", "model");
        var primary = new FakeCompletionService("primary") { CompleteResult = () => expected };
        var fallback = new FakeCompletionService("fallback");

        var result = await Build(primary, fallback).CompleteAsync(Request);

        Assert.Same(expected, result);
        Assert.False(fallback.WasCalled);
    }

    [Fact]
    public async Task CompleteAsync_WhenPrimaryThrowsProviderException_FallsBackToSecondary()
    {
        var expected = new CompletionResult("fallback answer", [], TokenUsage.Zero, "fallback", "model");
        var primary = new FakeCompletionService("primary") { CompleteThrows = new CompletionProviderException("primary", "boom") };
        var fallback = new FakeCompletionService("fallback") { CompleteResult = () => expected };

        var result = await Build(primary, fallback).CompleteAsync(Request);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task CompleteAsync_WhenPrimaryThrowsNonProviderException_PropagatesWithoutFallingBack()
    {
        var primary = new FakeCompletionService("primary") { CompleteThrows = new InvalidOperationException("a real bug, not a provider outage") };
        var fallback = new FakeCompletionService("fallback");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Build(primary, fallback).CompleteAsync(Request));

        Assert.False(fallback.WasCalled);
    }

    [Fact]
    public async Task StreamCompleteAsync_WhenPrimarySucceeds_YieldsOnlyPrimaryChunks()
    {
        var primary = new FakeCompletionService("primary")
        {
            StreamChunks = [new CompletionChunk("a", false), new CompletionChunk("b", true)]
        };
        var fallback = new FakeCompletionService("fallback");

        var chunks = await Collect(Build(primary, fallback).StreamCompleteAsync(Request));

        Assert.Equal(["a", "b"], chunks.Select(c => c.DeltaContent));
        Assert.False(fallback.WasCalled);
    }

    [Fact]
    public async Task StreamCompleteAsync_WhenPrimaryFailsBeforeAnyChunk_FallsBackAndYieldsFallbackChunks()
    {
        var primary = new FakeCompletionService("primary")
        {
            StreamChunks = [],
            StreamThrowsAfterChunks = new CompletionProviderException("primary", "down before first token")
        };
        var fallback = new FakeCompletionService("fallback")
        {
            StreamChunks = [new CompletionChunk("fallback-a", true)]
        };

        var chunks = await Collect(Build(primary, fallback).StreamCompleteAsync(Request));

        Assert.Equal(["fallback-a"], chunks.Select(c => c.DeltaContent));
    }

    [Fact]
    public async Task StreamCompleteAsync_WhenPrimaryFailsAfterYieldingChunks_PropagatesInsteadOfRestartingOnFallback()
    {
        // Restarting on the fallback here would re-send "a" to whatever already received it from
        // the primary (e.g. a client mid-SSE-stream) — that's a duplicated-output bug, not a
        // legitimate fallback, so this must propagate instead of silently switching providers.
        var primary = new FakeCompletionService("primary")
        {
            StreamChunks = [new CompletionChunk("a", false)],
            StreamThrowsAfterChunks = new CompletionProviderException("primary", "died mid-stream")
        };
        var fallback = new FakeCompletionService("fallback")
        {
            StreamChunks = [new CompletionChunk("fallback-a", true)]
        };

        var chunks = new List<CompletionChunk>();
        await Assert.ThrowsAsync<CompletionProviderException>(async () =>
        {
            await foreach (var chunk in Build(primary, fallback).StreamCompleteAsync(Request))
            {
                chunks.Add(chunk);
            }
        });

        Assert.Equal(["a"], chunks.Select(c => c.DeltaContent));
        Assert.False(fallback.WasCalled);
    }

    private static async Task<List<CompletionChunk>> Collect(IAsyncEnumerable<CompletionChunk> source)
    {
        var list = new List<CompletionChunk>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
