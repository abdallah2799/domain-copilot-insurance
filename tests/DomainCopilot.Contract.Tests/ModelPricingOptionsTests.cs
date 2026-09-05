using DomainCopilot.Infrastructure.Observability;

namespace DomainCopilot.Contract.Tests;

public class ModelPricingOptionsTests
{
    [Fact]
    public void EstimateCostUsd_ForAKnownModel_ComputesFromPerMillionTokenPrices()
    {
        var options = new ModelPricingOptions
        {
            Prices = { ["test-model"] = new ModelPrice(PromptPricePerMillionTokens: 1.00m, CompletionPricePerMillionTokens: 2.00m) },
        };

        var cost = options.EstimateCostUsd("test-model", promptTokens: 500_000, completionTokens: 250_000);

        Assert.Equal(0.50m + 0.50m, cost);
    }

    [Fact]
    public void EstimateCostUsd_ForAnUnknownModel_ReturnsZero_NotAGuess()
    {
        var options = new ModelPricingOptions();

        var cost = options.EstimateCostUsd("some-local-ollama-model", promptTokens: 100_000, completionTokens: 50_000);

        Assert.Equal(0m, cost);
    }

    [Fact]
    public void EstimateCostUsd_WithZeroTokens_ReturnsZero()
    {
        var options = new ModelPricingOptions();

        var cost = options.EstimateCostUsd("gpt-4o-mini", promptTokens: 0, completionTokens: 0);

        Assert.Equal(0m, cost);
    }
}
