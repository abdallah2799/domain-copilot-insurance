using Microsoft.Extensions.Configuration;

namespace DomainCopilot.Infrastructure.Observability;

public sealed record ModelPrice(decimal PromptPricePerMillionTokens, decimal CompletionPricePerMillionTokens);

/// <summary>Config-driven, not hardcoded per-provider logic -- a genuinely unknown/unlisted model
/// (e.g. a local Ollama model, which has no real dollar cost at all) prices at $0 rather than
/// guessing, which is the honest answer for this project's actual usage (OpenRouter's free-tier
/// model and every local Ollama model are genuinely $0; only a real OpenAI fallback call would ever
/// price above zero). <see cref="SectionName"/>'s entries are illustrative published list prices
/// for the one paid provider this project's fallback chain can reach (OpenAI), not verified against
/// a live pricing API -- documented as an estimate, per FR-9's own "estimated" framing.</summary>
public sealed class ModelPricingOptions
{
    public const string SectionName = "ModelPricing";

    public Dictionary<string, ModelPrice> Prices { get; set; } = new()
    {
        ["gpt-4o-mini"] = new ModelPrice(0.15m, 0.60m),
        ["text-embedding-3-small"] = new ModelPrice(0.02m, 0m),
    };

    public decimal EstimateCostUsd(string modelName, int promptTokens, int completionTokens)
    {
        if (!Prices.TryGetValue(modelName, out var price))
        {
            return 0m;
        }

        return promptTokens / 1_000_000m * price.PromptPricePerMillionTokens
            + completionTokens / 1_000_000m * price.CompletionPricePerMillionTokens;
    }

    public static ModelPricingOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new ModelPricingOptions();
        configuration.GetSection(SectionName).Bind(options);
        return options;
    }
}
