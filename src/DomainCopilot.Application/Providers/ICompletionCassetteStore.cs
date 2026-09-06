namespace DomainCopilot.Application.Providers;

/// <summary>
/// Persistence port for recorded provider exchanges. Kept as a port (rather than Application
/// touching the filesystem directly) for the same reason the prompt repository is one: where a
/// cassette lives is an Infrastructure concern, and tests substitute an in-memory store.
/// </summary>
public interface ICompletionCassetteStore
{
    Task<IReadOnlyList<CompletionCassetteEntry>> LoadAsync(CancellationToken cancellationToken = default);

    Task AppendAsync(CompletionCassetteEntry entry, CancellationToken cancellationToken = default);
}
