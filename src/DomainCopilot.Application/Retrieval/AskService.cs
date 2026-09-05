using System.Runtime.CompilerServices;
using System.Text.Json;
using DomainCopilot.Application.Adjudication;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Application.Retrieval;

/// <summary>FR-2/FR-6's "ask+citations": retrieval plus one grounded synthesis call on top,
/// deliberately not routed through <see cref="AgentRunner"/> — there is no tool-calling loop here
/// (a single retrieval call, then a single completion call with no tools), so the multi-turn
/// machinery that loop exists for doesn't apply. A refusal (FR-2's evidence-sufficiency signal)
/// short-circuits before any completion call is made, so a low-evidence question never costs LLM
/// spend just to be told no.</summary>
public sealed class AskService(HybridRetrievalService retrievalService, ICompletionService completionService, IPromptRepository prompts)
{
    private const string RefusalMessage = "The corpus doesn't have strong enough matching material to answer this question confidently.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AskResult> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        var retrieval = await retrievalService.SearchAsync(
            new RetrievalQuery(request.Question, request.TopK, request.DateOfLoss, request.FormVersion, request.Category),
            cancellationToken);

        if (!retrieval.HasSufficientEvidence)
        {
            return new AskResult(Refused: true, RefusalMessage, [], retrieval.Chunks);
        }

        var systemPrompt = await prompts.GetAsync("ask", cancellationToken);
        var passagesText = string.Join("\n\n", retrieval.Chunks.Select(c => $"[{CitationId(c)}] {c.Text}"));
        var userMessage = $"Question: \"{request.Question}\"\nRetrieved passages:\n{passagesText}";

        var completion = await completionService.CompleteAsync(new CompletionRequest([
            ChatMessage.System(systemPrompt),
            ChatMessage.User(userMessage),
        ]), cancellationToken);

        var (answer, citations) = ParseAnswer(completion.Content ?? string.Empty);
        return new AskResult(Refused: false, answer, citations, retrieval.Chunks);
    }

    /// <summary>FR-6's SSE token streaming: the same retrieval and refusal short-circuit as <see
    /// cref="AskAsync"/>, but a plain-prose streaming prompt (prompts/ask-stream.md) and one <see
    /// cref="AskStreamEvent.Delta"/> per token instead of a single JSON completion — see <see
    /// cref="AskStreamEventType.Done"/> for why its citation list isn't the model-selected subset
    /// <see cref="AskAsync"/> returns.</summary>
    public async IAsyncEnumerable<AskStreamEvent> AskStreamAsync(AskRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var retrieval = await retrievalService.SearchAsync(
            new RetrievalQuery(request.Question, request.TopK, request.DateOfLoss, request.FormVersion, request.Category),
            cancellationToken);

        if (!retrieval.HasSufficientEvidence)
        {
            yield return AskStreamEvent.Refused(RefusalMessage, retrieval.Chunks);
            yield break;
        }

        var systemPrompt = await prompts.GetAsync("ask-stream", cancellationToken);
        var passagesText = string.Join("\n\n", retrieval.Chunks.Select(c => $"[{CitationId(c)}] {c.Text}"));
        var userMessage = $"Question: \"{request.Question}\"\nRetrieved passages:\n{passagesText}";

        var stream = completionService.StreamCompleteAsync(new CompletionRequest([
            ChatMessage.System(systemPrompt),
            ChatMessage.User(userMessage),
        ]), cancellationToken);

        await foreach (var chunk in stream)
        {
            if (!string.IsNullOrEmpty(chunk.DeltaContent))
            {
                yield return AskStreamEvent.Delta(chunk.DeltaContent);
            }
        }

        yield return AskStreamEvent.Done(retrieval.Chunks);
    }

    internal static string CitationId(CitedChunk chunk) =>
        chunk.PageNumber is { } page ? $"{chunk.DocumentTitle}, {chunk.SectionTitle}, p.{page}" : $"{chunk.DocumentTitle}, {chunk.SectionTitle}";

    // Ask's prompt asks for one small JSON object with no preceding tool-call reasoning (unlike the
    // agent prompts AgentRunner's loop parses), so a direct parse with a code-fence strip is enough
    // robustness here; a model that still wraps it in prose falls back to the raw content as the
    // answer rather than failing the request outright.
    private static (string Answer, IReadOnlyList<string> Citations) ParseAnswer(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ParsedAnswer>(trimmed, JsonOptions);
            if (parsed is not null)
            {
                return (parsed.Answer, parsed.Citations ?? []);
            }
        }
        catch (JsonException)
        {
            // Fall through to the raw-content fallback below.
        }

        return (content.Trim(), []);
    }

    private sealed record ParsedAnswer(string Answer, IReadOnlyList<string>? Citations);
}
