namespace DomainCopilot.Infrastructure.Providers;

public sealed class OpenAiOptions
{
    public const string SectionName = "Providers:OpenAi";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

public sealed class OllamaOptions
{
    public const string SectionName = "Providers:Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.1";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    public Uri ChatCompletionEndpoint => new(new Uri(BaseUrl), "/v1");
}

/// <summary>
/// Hosted completion provider (see ADR-0003 update): OpenAI's API no longer has a perpetual free
/// tier, so OpenRouter — OpenAI-compatible, with genuinely free-tier models — is the default
/// primary completion provider instead. No embeddings endpoint, so it's completion-only.
/// </summary>
public sealed class OpenRouterOptions
{
    public const string SectionName = "Providers:OpenRouter";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "nvidia/nemotron-3.5-lightning:free";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public Uri ChatCompletionEndpoint => new(BaseUrl);
}
