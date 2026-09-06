using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Tests.Providers;

internal sealed class InMemoryCassetteStore : ICompletionCassetteStore
{
    private readonly List<CompletionCassetteEntry> entries = [];

    public IReadOnlyList<CompletionCassetteEntry> Entries => entries;

    public Task<IReadOnlyList<CompletionCassetteEntry>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CompletionCassetteEntry>>(entries);

    public Task AppendAsync(CompletionCassetteEntry entry, CancellationToken cancellationToken = default)
    {
        entries.Add(entry);
        return Task.CompletedTask;
    }
}

internal sealed class StubCompletionService(params CompletionResult[] results) : ICompletionService
{
    private int index;

    public int CallCount { get; private set; }

    public string ProviderName => "stub";

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(results[Math.Min(index++, results.Length - 1)]);
    }

    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CallCount++;
        var result = results[Math.Min(index++, results.Length - 1)];
        yield return new CompletionChunk(result.Content, IsFinal: false);
        yield return new CompletionChunk(null, IsFinal: true, result.Usage);
        await Task.CompletedTask;
    }
}

public class CompletionFingerprintTests
{
    private static CompletionRequest Request(string userMessage) =>
        new([ChatMessage.System("You are an agent."), ChatMessage.User(userMessage)]);

    [Fact]
    public void Compute_IdenticalRequests_ProduceTheSameFingerprint()
    {
        Assert.Equal(
            CompletionFingerprint.Compute(Request("Claim CLM-1")),
            CompletionFingerprint.Compute(Request("Claim CLM-1")));
    }

    [Fact]
    public void Compute_DifferentUserMessage_ProducesADifferentFingerprint()
    {
        Assert.NotEqual(
            CompletionFingerprint.Compute(Request("Claim CLM-1")),
            CompletionFingerprint.Compute(Request("Claim CLM-2")));
    }

    // The whole point of a cassette is that a replayed answer belongs to the exact conversation that
    // produced it -- a changed tool set can change the model's answer, so it must change the key.
    [Fact]
    public void Compute_DifferentToolsOffered_ProducesADifferentFingerprint()
    {
        var withTool = new CompletionRequest(
            [ChatMessage.User("Claim CLM-1")],
            [new ToolDefinition("resolve_policy_version", "Resolves the version.", "{}")]);

        Assert.NotEqual(
            CompletionFingerprint.Compute(new CompletionRequest([ChatMessage.User("Claim CLM-1")])),
            CompletionFingerprint.Compute(withTool));
    }

    [Fact]
    public void Compute_DifferentPriorToolResults_ProducesADifferentFingerprint()
    {
        CompletionRequest WithToolResult(string result) => new([
            ChatMessage.User("Claim CLM-1"),
            ChatMessage.Assistant("", [new ToolCall("call-1", "resolve_policy_version", "{}")]),
            ChatMessage.ToolResult("call-1", "resolve_policy_version", result),
        ]);

        Assert.NotEqual(
            CompletionFingerprint.Compute(WithToolResult("PAP-2024-STD")),
            CompletionFingerprint.Compute(WithToolResult("PAP-2025-STD")));
    }
}

public class RecordingCompletionServiceTests
{
    [Fact]
    public async Task CompleteAsync_RecordsTheRealProviderResponse_AndPassesItThrough()
    {
        var expected = new CompletionResult("Coverage confirmed.", [], new TokenUsage(120, 40), "openrouter", "test-model");
        var store = new InMemoryCassetteStore();
        var sut = new RecordingCompletionService(new StubCompletionService(expected), store);
        var request = new CompletionRequest([ChatMessage.User("Claim CLM-1")]);

        var result = await sut.CompleteAsync(request);

        Assert.Same(expected, result);
        var entry = Assert.Single(store.Entries);
        Assert.Equal(CompletionFingerprint.Compute(request), entry.Fingerprint);
        Assert.Equal("Coverage confirmed.", entry.Response.Content);
    }

    [Fact]
    public async Task StreamCompleteAsync_RecordsTheAssembledContent()
    {
        var store = new InMemoryCassetteStore();
        var sut = new RecordingCompletionService(
            new StubCompletionService(new CompletionResult("streamed answer", [], new TokenUsage(5, 5), "stub", "m")),
            store);

        await foreach (var _ in sut.StreamCompleteAsync(new CompletionRequest([ChatMessage.User("Q")]))) { }

        Assert.Equal("streamed answer", Assert.Single(store.Entries).Response.Content);
    }
}

public class ReplayCompletionServiceTests
{
    private static async Task<InMemoryCassetteStore> RecordedStoreAsync(CompletionRequest request, CompletionResult response)
    {
        var store = new InMemoryCassetteStore();
        await store.AppendAsync(new CompletionCassetteEntry(
            CompletionFingerprint.Compute(request), CompletionFingerprint.Preview(request), response));
        return store;
    }

    [Fact]
    public async Task CompleteAsync_ReturnsTheRecordedResponse_WithoutCallingAnyProvider()
    {
        var request = new CompletionRequest([ChatMessage.User("Claim CLM-1")]);
        var recorded = new CompletionResult("Recorded answer.", [], new TokenUsage(10, 20), "openrouter", "test-model");
        var sut = new ReplayCompletionService(await RecordedStoreAsync(request, recorded));

        var result = await sut.CompleteAsync(request);

        Assert.Equal("Recorded answer.", result.Content);
        Assert.Equal("replay", sut.ProviderName);
    }

    // Silently falling through to a live provider would both spend the free-tier budget replay
    // exists to protect and make a "replayed" run secretly non-deterministic -- so a miss is loud.
    [Fact]
    public async Task CompleteAsync_UnrecordedRequest_ThrowsWithReRecordingGuidance()
    {
        var sut = new ReplayCompletionService(await RecordedStoreAsync(
            new CompletionRequest([ChatMessage.User("Claim CLM-1")]),
            new CompletionResult("Recorded.", [], TokenUsage.Zero, "openrouter", "m")));

        var ex = await Assert.ThrowsAsync<CompletionProviderException>(
            () => sut.CompleteAsync(new CompletionRequest([ChatMessage.User("A question never recorded")])));

        Assert.Contains("No recorded response", ex.Message);
        Assert.Contains("Providers__CompletionMode=Record", ex.Message);
    }

    [Fact]
    public async Task CompleteAsync_ReplaysToolCallsNotJustText()
    {
        var request = new CompletionRequest([ChatMessage.User("Claim CLM-1")]);
        var recorded = new CompletionResult(
            null,
            [new ToolCall("call-1", "resolve_policy_version", """{"dateOfLoss":"2025-08-03"}""")],
            TokenUsage.Zero,
            "openrouter",
            "m");
        var sut = new ReplayCompletionService(await RecordedStoreAsync(request, recorded));

        var result = await sut.CompleteAsync(request);

        var toolCall = Assert.Single(result.ToolCalls);
        Assert.Equal("resolve_policy_version", toolCall.Name);
    }

    [Fact]
    public async Task StreamCompleteAsync_ReChunksRecordedContentSoStreamingStillStreams()
    {
        var request = new CompletionRequest([ChatMessage.User("Q")]);
        var longAnswer = new string('x', 100);
        var sut = new ReplayCompletionService(await RecordedStoreAsync(
            request, new CompletionResult(longAnswer, [], new TokenUsage(1, 2), "openrouter", "m")));

        var chunks = new List<CompletionChunk>();
        await foreach (var chunk in sut.StreamCompleteAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.True(chunks.Count > 2, "a 100-character answer should arrive as several deltas, not one");
        Assert.Equal(longAnswer, string.Concat(chunks.Select(c => c.DeltaContent)));
        Assert.True(chunks[^1].IsFinal);
    }
}
