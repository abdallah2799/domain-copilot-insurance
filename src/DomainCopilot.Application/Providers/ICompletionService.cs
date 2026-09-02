namespace DomainCopilot.Application.Providers;

/// <summary>
/// Port covering completion, streaming, and tool-calling for a single LLM provider. Infrastructure
/// provides one implementation per provider (OpenAI, Ollama, ...); Application/Api never reference
/// a provider SDK directly, only this interface.
/// </summary>
public interface ICompletionService
{
    string ProviderName { get; }

    Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default);
}
