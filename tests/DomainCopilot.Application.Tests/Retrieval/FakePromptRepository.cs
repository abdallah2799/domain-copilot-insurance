using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Application.Tests.Retrieval;

internal sealed class FakePromptRepository(string prompt = "fake system prompt") : IPromptRepository
{
    public Task<string> GetAsync(string promptName, CancellationToken cancellationToken = default) => Task.FromResult(prompt);
}
