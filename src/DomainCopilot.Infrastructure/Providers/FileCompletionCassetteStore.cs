using System.Text.Json;
using System.Text.Json.Serialization;
using DomainCopilot.Application.Providers;

namespace DomainCopilot.Infrastructure.Providers;

/// <summary>How the completion chain behaves (ADR-0014). <see cref="Live"/> calls the real provider
/// chain; <see cref="Record"/> calls it and saves every exchange to a cassette; <see cref="Replay"/>
/// serves those saved exchanges and makes no network call at all.</summary>
public enum CompletionMode
{
    Live,
    Record,
    Replay,
}

public sealed class CassetteOptions
{
    public const string SectionName = "Providers:Cassette";

    /// <summary>Path to the cassette file, absolute or relative to the repository root.</summary>
    public string Path { get; set; } = "seed-data/cassettes/adjudication-demo.json";
}

/// <summary>
/// Stores recorded provider exchanges as one indented JSON file, deliberately committed to the
/// repository: a cassette is review-able evidence of what the model actually returned, and being
/// able to diff it is the point. Rewrites the whole file on append rather than streaming into it —
/// a cassette holds tens of entries, so simplicity beats an append-safe JSON writer here.
/// </summary>
public sealed class FileCompletionCassetteStore : ICompletionCassetteStore
{
    private static readonly JsonSerializerOptions FileJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path;
    private List<CompletionCassetteEntry>? cached;

    public FileCompletionCassetteStore(CassetteOptions options)
    {
        path = RepositoryRoot.Resolve(options.Path);
    }

    public async Task<IReadOnlyList<CompletionCassetteEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadUnsynchronizedAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AppendAsync(CompletionCassetteEntry entry, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadUnsynchronizedAsync(cancellationToken);
            entries.Add(entry);

            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(entries, FileJson), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<CompletionCassetteEntry>> LoadUnsynchronizedAsync(CancellationToken cancellationToken)
    {
        if (cached is not null)
        {
            return cached;
        }

        if (!File.Exists(path))
        {
            cached = [];
            return cached;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        cached = string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<CompletionCassetteEntry>>(json, FileJson) ?? [];
        return cached;
    }
}
