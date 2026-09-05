using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.Tests.Adjudication;
using DomainCopilot.Application.Tests.Observability;
using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Tests.Retrieval;

public class AskServiceTests
{
    private static CompletionResult FinalAnswer(string content) => new(content, [], TokenUsage.Zero, "fake", "fake-model");

    private static Document CompletedDocument(string title)
    {
        var document = Document.Create(Guid.NewGuid().ToString("N"), title, DocumentCategory.Reference, DocumentFormat.Pdf, "reference/x.pdf", "hash");
        document.BeginProcessing();
        document.MarkCompleted(1);
        return document;
    }

    private static (AskService Service, FakeDocumentRepository Documents, FakeVectorStore VectorStore, FakeKeywordSearchIndex KeywordIndex, SequencedFakeCompletionService Completion, FakeTokenUsageRecorder TokenUsage) BuildService()
    {
        var documents = new FakeDocumentRepository();
        var vectorStore = new FakeVectorStore();
        var keywordIndex = new FakeKeywordSearchIndex();
        var retrieval = new HybridRetrievalService(documents, new FakeEmbeddingService(), vectorStore, keywordIndex);
        var completion = new SequencedFakeCompletionService();
        var tokenUsage = new FakeTokenUsageRecorder();
        var service = new AskService(retrieval, completion, new FakePromptRepository(), tokenUsage);
        return (service, documents, vectorStore, keywordIndex, completion, tokenUsage);
    }

    private static (AskService Service, FakeDocumentRepository Documents, FakeVectorStore VectorStore, FakeKeywordSearchIndex KeywordIndex, FakeTokenUsageRecorder TokenUsage) BuildStreamingService(FakeStreamingCompletionService completion)
    {
        var documents = new FakeDocumentRepository();
        var vectorStore = new FakeVectorStore();
        var keywordIndex = new FakeKeywordSearchIndex();
        var retrieval = new HybridRetrievalService(documents, new FakeEmbeddingService(), vectorStore, keywordIndex);
        var tokenUsage = new FakeTokenUsageRecorder();
        var service = new AskService(retrieval, completion, new FakePromptRepository(), tokenUsage);
        return (service, documents, vectorStore, keywordIndex, tokenUsage);
    }

    private static (Document Document, ScoredChunk Chunk) SeedOneChunk(FakeDocumentRepository documents, FakeVectorStore vectorStore, FakeKeywordSearchIndex keywordIndex, string title = "Some Reference")
    {
        var document = CompletedDocument(title);
        documents.Seed(document);
        var chunk = new ScoredChunk(document.Id, 0, "Intro", null, DocumentCategory.Reference, null, null, "Some grounded text.", Score: 0.9);
        vectorStore.SeedSearchResults([chunk]);
        keywordIndex.SeedSearchResults([chunk]);
        return (document, chunk);
    }

    [Fact]
    public async Task AskAsync_NoDenseMatch_RefusesWithoutCallingCompletion()
    {
        var (service, _, _, _, completion, _) = BuildService();
        // No search results seeded on either leg -- HybridRetrievalService's own refusal path
        // (empty Chunks) fires without any dense score to evaluate.

        var result = await service.AskAsync(new AskRequest("Does the glass waiver apply to collision?"));

        Assert.True(result.Refused);
        Assert.Empty(result.Citations);
        Assert.Equal(0, completion.CallCount);
    }

    [Fact]
    public async Task AskAsync_SufficientEvidence_ParsesGroundedAnswerAndCitations()
    {
        var (service, documents, vectorStore, keywordIndex, completion, _) = BuildService();
        var document = CompletedDocument("Policy Wording PAP-2025-STD");
        documents.Seed(document);

        var chunk = new ScoredChunk(document.Id, 0, "Section 5.4", 12, DocumentCategory.PolicyForm, "PAP-2025-STD", new DateOnly(2025, 6, 1),
            "The glass-only deductible waiver applies exclusively to Comprehensive losses.", Score: 0.9);
        vectorStore.SeedSearchResults([chunk]);
        keywordIndex.SeedSearchResults([chunk]);

        completion.Enqueue(() => FinalAnswer(
            """{"answer":"No, it only applies to Comprehensive losses.","citations":["Policy Wording PAP-2025-STD, Section 5.4, p.12"]}"""));

        var result = await service.AskAsync(new AskRequest("Does the glass waiver apply to collision?"));

        Assert.False(result.Refused);
        Assert.Equal("No, it only applies to Comprehensive losses.", result.Answer);
        Assert.Equal(["Policy Wording PAP-2025-STD, Section 5.4, p.12"], result.Citations);
        Assert.Single(result.RetrievedChunks);
        Assert.Equal(1, completion.CallCount);
    }

    [Fact]
    public async Task AskAsync_SufficientEvidence_RecordsRealTokenUsage()
    {
        var (service, documents, vectorStore, keywordIndex, completion, tokenUsage) = BuildService();
        SeedOneChunk(documents, vectorStore, keywordIndex);
        completion.Enqueue(() => new CompletionResult("""{"answer":"An answer.","citations":[]}""", [], new TokenUsage(200, 40), "fake-provider", "fake-model"));

        await service.AskAsync(new AskRequest("some question"));

        var recorded = Assert.Single(tokenUsage.RecordedEntries);
        Assert.Equal("Ask", recorded.AgentName);
        Assert.Equal("fake-provider", recorded.ProviderName);
        Assert.Equal("fake-model", recorded.ModelName);
        Assert.Equal(200, recorded.PromptTokens);
        Assert.Equal(40, recorded.CompletionTokens);
    }

    [Fact]
    public async Task AskAsync_Refused_RecordsNoTokenUsage_NoCompletionCallWasMade()
    {
        var (service, _, _, _, _, tokenUsage) = BuildService();

        await service.AskAsync(new AskRequest("out of corpus question"));

        Assert.Empty(tokenUsage.RecordedEntries);
    }

    [Fact]
    public async Task AskAsync_AnswerWrappedInCodeFence_StillParses()
    {
        var (service, documents, vectorStore, keywordIndex, completion, _) = BuildService();
        var document = CompletedDocument("Some Reference");
        documents.Seed(document);
        var chunk = new ScoredChunk(document.Id, 0, "Intro", null, DocumentCategory.Reference, null, null, "Some grounded text.", Score: 0.9);
        vectorStore.SeedSearchResults([chunk]);
        keywordIndex.SeedSearchResults([chunk]);

        completion.Enqueue(() => FinalAnswer("```json\n{\"answer\":\"Fenced answer.\",\"citations\":[]}\n```"));

        var result = await service.AskAsync(new AskRequest("some question"));

        Assert.False(result.Refused);
        Assert.Equal("Fenced answer.", result.Answer);
    }

    [Fact]
    public async Task AskAsync_NonJsonCompletion_FallsBackToRawContentRatherThanThrowing()
    {
        var (service, documents, vectorStore, keywordIndex, completion, _) = BuildService();
        var document = CompletedDocument("Some Reference");
        documents.Seed(document);
        var chunk = new ScoredChunk(document.Id, 0, "Intro", null, DocumentCategory.Reference, null, null, "Some grounded text.", Score: 0.9);
        vectorStore.SeedSearchResults([chunk]);
        keywordIndex.SeedSearchResults([chunk]);

        completion.Enqueue(() => FinalAnswer("The answer is just plain prose, not JSON."));

        var result = await service.AskAsync(new AskRequest("some question"));

        Assert.False(result.Refused);
        Assert.Equal("The answer is just plain prose, not JSON.", result.Answer);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task AskStreamAsync_NoDenseMatch_YieldsOnlyRefusedEvent()
    {
        var completion = new FakeStreamingCompletionService(["should never be reached"]);
        var (service, _, _, _, _) = BuildStreamingService(completion);

        var events = new List<AskStreamEvent>();
        await foreach (var evt in service.AskStreamAsync(new AskRequest("out of corpus question")))
        {
            events.Add(evt);
        }

        var single = Assert.Single(events);
        Assert.Equal(AskStreamEventType.Refused, single.Type);
        Assert.Equal(0, completion.DeltasYielded);
    }

    [Fact]
    public async Task AskStreamAsync_SufficientEvidence_YieldsDeltasThenDoneWithAllRetrievedCitations()
    {
        var completion = new FakeStreamingCompletionService(["No, ", "it only ", "applies to Comprehensive."]);
        var (service, documents, vectorStore, keywordIndex, _) = BuildStreamingService(completion);
        SeedOneChunk(documents, vectorStore, keywordIndex, "Policy Wording PAP-2025-STD");

        var events = new List<AskStreamEvent>();
        await foreach (var evt in service.AskStreamAsync(new AskRequest("Does the glass waiver apply to collision?")))
        {
            events.Add(evt);
        }

        Assert.Equal(4, events.Count);
        Assert.Equal(["No, ", "it only ", "applies to Comprehensive."], events.Take(3).Select(e => e.DeltaText));
        Assert.All(events.Take(3), e => Assert.Equal(AskStreamEventType.Delta, e.Type));

        var done = events[^1];
        Assert.Equal(AskStreamEventType.Done, done.Type);
        Assert.Single(done.Citations!);
        Assert.Contains("Policy Wording PAP-2025-STD", done.Citations!.Single());
    }

    [Fact]
    public async Task AskStreamAsync_SufficientEvidence_RecordsRealTokenUsageFromTheFinalChunk()
    {
        var completion = new FakeStreamingCompletionService(["No, ", "it does not."], finalUsage: new TokenUsage(180, 25));
        var (service, documents, vectorStore, keywordIndex, tokenUsage) = BuildStreamingService(completion);
        SeedOneChunk(documents, vectorStore, keywordIndex);

        await foreach (var _ in service.AskStreamAsync(new AskRequest("some question")))
        {
        }

        var recorded = Assert.Single(tokenUsage.RecordedEntries);
        Assert.Equal("AskStream", recorded.AgentName);
        Assert.Equal("fake-streaming", recorded.ProviderName);
        Assert.Equal(180, recorded.PromptTokens);
        Assert.Equal(25, recorded.CompletionTokens);
    }

    [Fact]
    public async Task AskStreamAsync_CancelledMidStream_StopsEnumeratingRatherThanYieldingAllDeltas()
    {
        var completion = new FakeStreamingCompletionService(["one", "two", "three", "four", "five"]);
        var (service, documents, vectorStore, keywordIndex, _) = BuildStreamingService(completion);
        SeedOneChunk(documents, vectorStore, keywordIndex);

        using var cts = new CancellationTokenSource();
        var events = new List<AskStreamEvent>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var evt in service.AskStreamAsync(new AskRequest("some question"), cts.Token))
            {
                events.Add(evt);
                if (events.Count == 2)
                {
                    cts.Cancel();
                }
            }
        });

        // Cancellation stopped enumeration partway through -- the real streaming providers honor
        // the same token all the way into their HTTP call, so this is the same contract, just
        // exercised against a fake that can prove it deterministically rather than timing-depend on
        // a real network call.
        Assert.True(completion.DeltasYielded < 5);
    }
}
