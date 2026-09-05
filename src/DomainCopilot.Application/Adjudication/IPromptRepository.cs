namespace DomainCopilot.Application.Adjudication;

/// <summary>Port over versioned prompt files (CLAUDE.md: "Prompts live under prompts/ as versioned
/// files, never as string literals in C#"). Infrastructure reads them from disk; Application never
/// touches the filesystem directly.</summary>
public interface IPromptRepository
{
    Task<string> GetAsync(string promptName, CancellationToken cancellationToken = default);
}
