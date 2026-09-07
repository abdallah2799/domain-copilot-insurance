namespace DomainCopilot.Application.Providers;

/// <summary>
/// Raised by an <see cref="ICompletionService"/>/<see cref="IEmbeddingService"/> adapter when a
/// provider call fails. <see cref="ProviderName"/> lets a fallback chain log which leg failed
/// without the Application layer knowing anything about the underlying SDK.
///
/// <see cref="IsRateLimited"/> distinguishes "this provider is temporarily out of budget" from
/// "this provider is broken", because the right response differs: a rate limit clears on its own if
/// you wait, whereas failing straight over to the next leg spends a scarcer budget (or drops onto a
/// far slower local model) for a condition that would have resolved in seconds.
/// </summary>
public sealed class CompletionProviderException(
    string providerName,
    string message,
    Exception? innerException = null,
    bool isRateLimited = false,
    TimeSpan? retryAfter = null,
    bool isTransient = false)
    : Exception(message, innerException)
{
    public string ProviderName { get; } = providerName;

    public bool IsRateLimited { get; } = isRateLimited;

    /// <summary>How long the provider asked the caller to wait, when it said so.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;

    /// <summary>
    /// A failure that is a property of this one attempt rather than of the provider — most notably
    /// a client-library defect parsing an otherwise valid response, where simply asking again
    /// usually succeeds because the next response has a different shape. Separate from
    /// <see cref="IsRateLimited"/>, which also clears on its own but needs a much longer wait.
    /// </summary>
    public bool IsTransient { get; } = isTransient;
}
