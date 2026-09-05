using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.Tests.Adjudication;
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

    private static (AskService Service, FakeDocumentRepository Documents, FakeVectorStore VectorStore, FakeKeywordSearchIndex KeywordIndex, SequencedFakeCompletionService Completion) BuildService()
    {
        var documents = new FakeDocumentRepository();
        var vectorStore = new FakeVectorStore();
        var keywordIndex = new FakeKeywordSearchIndex();
        var retrieval = new HybridRetrievalService(documents, new FakeEmbeddingService(), vectorStore, keywordIndex);
        var completion = new SequencedFakeCompletionService();
        var service = new AskService(retrieval, completion, new FakePromptRepository());
        return (service, documents, vectorStore, keywordIndex, completion);
    }

    [Fact]
    public async Task AskAsync_NoDenseMatch_RefusesWithoutCallingCompletion()
    {
        var (service, _, _, _, completion) = BuildService();
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
        var (service, documents, vectorStore, keywordIndex, completion) = BuildService();
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
    public async Task AskAsync_AnswerWrappedInCodeFence_StillParses()
    {
        var (service, documents, vectorStore, keywordIndex, completion) = BuildService();
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
        var (service, documents, vectorStore, keywordIndex, completion) = BuildService();
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
}
