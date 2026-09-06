using System.Collections.Concurrent;
using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>Reads versioned prompt files from disk (one file per agent, named
/// <c>{promptName}.md</c> under <see cref="PromptOptions.Directory"/>), cached after first read
/// since prompt content is static for the lifetime of the process.
///
/// The configured directory is resolved against the repository root rather than the working
/// directory: it defaults to the relative path <c>prompts</c>, and starting the API the way the
/// README documents (<c>dotnet run --project src/DomainCopilot.Api</c>) makes the working directory
/// that project's own folder, where no <c>prompts</c> directory exists. That made every agent fail
/// with a FileNotFoundException the moment a run started — the whole four-agent workflow, not a
/// degraded corner of it.</summary>
public sealed class FilePromptRepository(PromptOptions options) : IPromptRepository
{
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly string _directory = RepositoryRoot.Resolve(options.Directory);

    public Task<string> GetAsync(string promptName, CancellationToken cancellationToken = default)
    {
        var content = _cache.GetOrAdd(promptName, name =>
        {
            var path = Path.Combine(_directory, $"{name}.md");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"No prompt file found for '{name}' at '{path}'.", path);
            }

            return File.ReadAllText(path);
        });

        return Task.FromResult(content);
    }
}
