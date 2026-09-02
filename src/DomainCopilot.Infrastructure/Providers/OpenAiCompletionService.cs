using DomainCopilot.Application.Providers;
using Microsoft.SemanticKernel;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>Hosted provider leg of the fallback chain — see ADR on provider abstraction.</summary>
public sealed class OpenAiCompletionService : ICompletionService
{
    private readonly SemanticKernelCompletionAdapter _adapter;

    public OpenAiCompletionService(OpenAiOptions options)
    {
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(options.Model, options.ApiKey)
            .Build();

        _adapter = new SemanticKernelCompletionAdapter(ProviderName, options.Model, kernel);
    }

    public string ProviderName => "OpenAI";

    public Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default) =>
        _adapter.StreamCompleteAsync(request, cancellationToken);
}
