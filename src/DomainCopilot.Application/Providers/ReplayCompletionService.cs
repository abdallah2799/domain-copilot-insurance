using System.Runtime.CompilerServices;

namespace DomainCopilot.Application.Providers;

/// <summary>
/// Serves previously recorded provider responses from a cassette, making no network call at all.
/// Exists because the hosted free tier this project targets allows roughly 50 requests per day
/// while one full four-agent adjudication run costs up to ~30 — so a live provider affords about
/// two end-to-end runs a day, which is not enough to develop against, test in CI, or rehearse a
/// demo. A cassette turns one real run into unlimited identical replays.
///
/// A request that was never recorded is a hard error rather than a silent pass-through to a live
/// provider: replaying is supposed to be hermetic, and quietly reaching the network would both
/// spend the very budget this class protects and make a "replayed" run secretly non-deterministic.
/// </summary>
public sealed class ReplayCompletionService(ICompletionCassetteStore store) : ICompletionService
{
    // Replay is read-only over a cassette that is written before the process starts, so loading it
    // once and holding it is safe; the lazy keeps startup from paying for a file read it may never
    // need (the mode is chosen by configuration, but the service is still constructed by DI).
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyDictionary<string, CompletionResult>? entries;

    public string ProviderName => "replay";

    public async Task<CompletionResult> CompleteAsync(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        var cassette = await LoadAsync(cancellationToken);
        var fingerprint = CompletionFingerprint.Compute(request);

        if (!cassette.TryGetValue(fingerprint, out var result))
        {
            throw new CompletionProviderException(
                ProviderName,
                $"No recorded response for this request (fingerprint {fingerprint[..12]}…, {CompletionFingerprint.Preview(request)}). " +
                "The cassette was recorded against different inputs or a since-changed prompt — re-record it with Providers__CompletionMode=Record.");
        }

        return result;
    }

    /// <summary>Re-chunks the recorded content so a replayed answer still arrives incrementally and
    /// the streaming UI behaves exactly as it does against a live provider.</summary>
    public async IAsyncEnumerable<CompletionChunk> StreamCompleteAsync(
        CompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await CompleteAsync(request, cancellationToken);
        var content = result.Content ?? string.Empty;

        const int chunkSize = 24;
        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new CompletionChunk(content.Substring(offset, Math.Min(chunkSize, content.Length - offset)), IsFinal: false);
        }

        yield return new CompletionChunk(null, IsFinal: true, result.Usage);
    }

    private async Task<IReadOnlyDictionary<string, CompletionResult>> LoadAsync(CancellationToken cancellationToken)
    {
        if (entries is not null)
        {
            return entries;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (entries is null)
            {
                var loaded = await store.LoadAsync(cancellationToken);
                // A later recording of the same fingerprint wins, so re-recording a single exchange
                // by appending doesn't require pruning the earlier one by hand first.
                entries = loaded
                    .GroupBy(e => e.Fingerprint)
                    .ToDictionary(g => g.Key, g => g.Last().Response);
            }
        }
        finally
        {
            gate.Release();
        }

        return entries;
    }
}
