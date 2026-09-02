namespace DomainCopilot.Application.Providers;

public sealed record TokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;

    public static readonly TokenUsage Zero = new(0, 0);
}
