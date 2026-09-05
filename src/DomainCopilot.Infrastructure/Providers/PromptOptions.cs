namespace DomainCopilot.Infrastructure.Providers;

public sealed class PromptOptions
{
    public const string SectionName = "Prompts";

    public string Directory { get; set; } = "prompts";
}
