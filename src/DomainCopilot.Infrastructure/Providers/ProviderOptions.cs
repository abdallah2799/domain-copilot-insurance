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
