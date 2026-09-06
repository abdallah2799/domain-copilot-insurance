namespace DomainCopilot.Infrastructure;

/// <summary>
/// Locates the repository root by searching upward from the running assembly for the solution file.
///
/// Needed because several configured paths are relative by default (the prompt directory, the
/// provider cassette) and the process's working directory is not a reliable base for them:
/// <c>dotnet run --project src/DomainCopilot.Api</c> — the command the README itself documents —
/// sets the working directory to that project's own folder rather than the repository root. The
/// same mistake previously broke <c>.env</c> loading; resolving against the assembly's location
/// instead works regardless of how or from where the process was started.
/// </summary>
public static class RepositoryRoot
{
    private const string SolutionFileName = "DomainCopilot.slnx";

    public static string? Find(string? startDirectory = null)
    {
        var dir = startDirectory ?? AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, SolutionFileName)))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        return null;
    }

    /// <summary>Falls back to the current directory when no solution file is found — the published
    /// containerised case, where the app's content root is already the right base.</summary>
    public static string FindOrCurrent(string? startDirectory = null) =>
        Find(startDirectory) ?? Directory.GetCurrentDirectory();

    /// <summary>Resolves a possibly-relative configured path against the repository root.</summary>
    public static string Resolve(string configuredPath) =>
        Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(FindOrCurrent(), configuredPath);
}
