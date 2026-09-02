namespace DomainCopilot.Application.Providers;

/// <summary>
/// Raised by an <see cref="ICompletionService"/>/<see cref="IEmbeddingService"/> adapter when a
/// provider call fails. <see cref="ProviderName"/> lets a fallback chain log which leg failed
/// without the Application layer knowing anything about the underlying SDK.
/// </summary>
public sealed class CompletionProviderException(string providerName, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ProviderName { get; } = providerName;
}
