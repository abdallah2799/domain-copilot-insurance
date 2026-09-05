using DomainCopilot.Application.Observability;

namespace DomainCopilot.Application.Tests.Observability;

internal sealed class FakeTokenUsageRecorder : ITokenUsageRecorder
{
    public List<TokenUsageEntry> RecordedEntries { get; } = [];

    public Task RecordAsync(TokenUsageEntry entry, CancellationToken cancellationToken = default)
    {
        RecordedEntries.Add(entry);
        return Task.CompletedTask;
    }
}
