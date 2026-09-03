using DomainCopilot.Application.Documents;
using DomainCopilot.Application.Providers;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Providers;
using DomainCopilot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;

namespace DomainCopilot.Infrastructure;

/// <summary>Composition-root wiring for Infrastructure. Called once from Api's Program.cs.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDomainCopilotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DomainCopilotDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        var qdrantOptions = configuration.GetSection(QdrantOptions.SectionName).Get<QdrantOptions>() ?? new QdrantOptions();
        services.AddSingleton(qdrantOptions);
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<QdrantOptions>();
            return new QdrantClient(opts.Host, opts.GrpcPort, opts.Https, opts.ApiKey);
        });

        // Readiness (not liveness — see Program.cs) checks for both stateful dependencies, so
        // /health/ready actually proves the app can reach its data stores, not just that the
        // process started.
        services.AddHealthChecks()
            .AddDbContextCheck<DomainCopilotDbContext>("mssql", tags: ["ready"])
            .AddCheck<QdrantHealthCheck>("qdrant", tags: ["ready"]);

        var openAiOptions = configuration.GetSection(OpenAiOptions.SectionName).Get<OpenAiOptions>() ?? new OpenAiOptions();
        var ollamaOptions = configuration.GetSection(OllamaOptions.SectionName).Get<OllamaOptions>() ?? new OllamaOptions();
        var openRouterOptions = configuration.GetSection(OpenRouterOptions.SectionName).Get<OpenRouterOptions>() ?? new OpenRouterOptions();

        services.AddSingleton(openAiOptions);
        services.AddSingleton(ollamaOptions);
        services.AddSingleton(openRouterOptions);

        services.AddSingleton<OpenAiCompletionService>();
        services.AddSingleton<OllamaCompletionService>();
        services.AddSingleton<OpenRouterCompletionService>();
        services.AddSingleton<OpenAiEmbeddingService>();
        services.AddSingleton<OllamaEmbeddingService>();

        // Completions: OpenRouter (hosted, primary) -> Ollama (local, fallback). OpenAI's API no
        // longer has a perpetual free tier; OpenRouter does (see ADR-0003 update), at the cost of a
        // tight free-tier rate limit (20 req/min, 50 req/day without a credit purchase) — which is
        // exactly why the Ollama fallback leg matters here, not just as a formality.
        services.AddSingleton<ICompletionService>(sp => new FallbackCompletionService(
            sp.GetRequiredService<OpenRouterCompletionService>(),
            sp.GetRequiredService<OllamaCompletionService>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FallbackCompletionService>>()));

        // Embeddings: Ollama (local, primary) -> OpenAI (hosted, fallback). OpenRouter has no
        // embeddings endpoint, so it isn't part of this chain at all. Ollama is primary here
        // (rather than fallback, as for completions) because embeddings are cheap to run entirely
        // locally and there's no free-tier request budget to conserve by preferring a hosted call.
        services.AddSingleton<IEmbeddingService>(sp => new FallbackEmbeddingService(
            sp.GetRequiredService<OllamaEmbeddingService>(),
            sp.GetRequiredService<OpenAiEmbeddingService>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FallbackEmbeddingService>>()));

        return services;
    }
}
