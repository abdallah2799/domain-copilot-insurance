using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DomainCopilot.Infrastructure;

/// <summary>Composition-root wiring for Infrastructure. Called once from Api's Program.cs.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDomainCopilotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var openAiOptions = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>() ?? new OpenAiOptions();
        var ollamaOptions = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>() ?? new OllamaOptions();

        services.AddSingleton(openAiOptions);
        services.AddSingleton(ollamaOptions);

        services.AddSingleton<OpenAiCompletionService>();
        services.AddSingleton<OllamaCompletionService>();
        services.AddSingleton<OpenAiEmbeddingService>();
        services.AddSingleton<OllamaEmbeddingService>();

        // Primary/fallback order is fixed to OpenAI -> Ollama here (documented fallback chain, ADR
        // pending); swapping either leg to a different provider is a new Infrastructure adapter
        // class plus this one registration changing, not a change to any Application/Api code.
        services.AddSingleton<ICompletionService>(sp => new FallbackCompletionService(
            sp.GetRequiredService<OpenAiCompletionService>(),
            sp.GetRequiredService<OllamaCompletionService>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FallbackCompletionService>>()));

        services.AddSingleton<IEmbeddingService>(sp => new FallbackEmbeddingService(
            sp.GetRequiredService<OpenAiEmbeddingService>(),
            sp.GetRequiredService<OllamaEmbeddingService>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FallbackEmbeddingService>>()));

        return services;
    }
}
