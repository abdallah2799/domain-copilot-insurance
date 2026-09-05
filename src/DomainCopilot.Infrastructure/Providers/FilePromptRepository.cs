using System.Collections.Concurrent;
using DomainCopilot.Application.Adjudication;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>Reads versioned prompt files from disk (one file per agent, named
/// <c>{promptName}.md</c> under <see cref="PromptOptions.Directory"/>), cached after first read
/// since prompt content is static for the lifetime of the process.</summary>
public sealed class FilePromptRepository(PromptOptions options) : IPromptRepository
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public Task<string> GetAsync(string promptName, CancellationToken cancellationToken = default)
    {
        var content = _cache.GetOrAdd(promptName, name =>
        {
            var path = Path.Combine(options.Directory, $"{name}.md");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"No prompt file found for '{name}' at '{path}'.", path);
            }

            return File.ReadAllText(path);
        });

        return Task.FromResult(content);
    }
}
