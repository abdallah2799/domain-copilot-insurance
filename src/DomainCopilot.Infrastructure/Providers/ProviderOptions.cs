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
/// Primary hosted completion provider (see ADR-0003). OpenAI-compatible, completion-only (Groq has
/// no embeddings endpoint). Chosen over OpenRouter as the primary leg because OpenRouter's free
/// tier caps at 50 model requests per day and one four-agent run can cost ~30 of them.
/// </summary>
public sealed class GroqOptions
{
    public const string SectionName = "Providers:Groq";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Must be a Groq model that supports tool calling — every agent in this project
    /// depends on it. See https://console.groq.com/docs/models for the current list.</summary>
    public string Model { get; set; } = "openai/gpt-oss-120b";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public Uri ChatCompletionEndpoint => new(BaseUrl);
}

/// <summary>
/// Second hosted completion leg (see ADR-0003 update): OpenAI's API no longer has a perpetual free
/// tier, so OpenRouter — OpenAI-compatible, with genuinely free-tier models — backs up Groq before
/// the chain falls all the way to a local model. No embeddings endpoint, so it's completion-only.
/// </summary>
public sealed class OpenRouterOptions
{
    public const string SectionName = "Providers:OpenRouter";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "nvidia/nemotron-3-super-120b-a12b:free";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public Uri ChatCompletionEndpoint => new(BaseUrl);
}
