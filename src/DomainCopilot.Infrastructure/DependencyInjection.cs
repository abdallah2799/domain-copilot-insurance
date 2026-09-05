using DomainCopilot.Application.Adjudication;
using DomainCopilot.Application.CaseData;
using DomainCopilot.Application.Documents;
using DomainCopilot.Application.Identity;
using DomainCopilot.Application.Ingestion;
using DomainCopilot.Application.Observability;
using DomainCopilot.Application.Ocr;
using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.VectorStore;
using DomainCopilot.Infrastructure.Adjudication;
using DomainCopilot.Infrastructure.Identity;
using DomainCopilot.Infrastructure.Ingestion;
using DomainCopilot.Infrastructure.Observability;
using DomainCopilot.Infrastructure.Ocr;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.Adjudication;
using DomainCopilot.Infrastructure.Persistence.CaseData;
using DomainCopilot.Infrastructure.Persistence.Identity;
using DomainCopilot.Infrastructure.Persistence.Ocr;
using DomainCopilot.Infrastructure.Providers;
using DomainCopilot.Infrastructure.Retrieval;
using DomainCopilot.Infrastructure.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qdrant.Client;
using IVectorStore = DomainCopilot.Application.VectorStore.IVectorStore;

namespace DomainCopilot.Infrastructure;

/// <summary>Composition-root wiring for Infrastructure. Called once from Api's Program.cs.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDomainCopilotInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // A factory, not AddDbContext directly: FR-9's token-usage recorder needs its own
        // short-lived context per write (see EfTokenUsageRecorder), independent of whatever the
        // ambient per-request scoped context happens to be doing -- registering both AddDbContext
        // and AddDbContextFactory for the same TContext produces a lifetime conflict (a singleton
        // factory can't consume the scoped DbContextOptions AddDbContext registers), so the scoped
        // DomainCopilotDbContext everything else here injects is instead created from this same
        // factory, per Microsoft's documented pattern for combining the two.
        services.AddDbContextFactory<DomainCopilotDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<DomainCopilotDbContext>>().CreateDbContext());
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IPolicyDeclarationRepository, PolicyDeclarationRepository>();
        services.AddScoped<IClaimHistoryRepository, ClaimHistoryRepository>();
        services.AddScoped<CaseDataLoadingService>();
        services.AddScoped<IAdjudicationCaseRepository, AdjudicationCaseRepository>();

        // FR-8 (ADR-0012): PBKDF2 hashing (BCL only), JWT issuance, and a startup seeder for the two
        // demo accounts (one per role) this project ships with instead of self-service registration.
        var authOptions = AuthOptions.FromConfiguration(configuration);
        services.AddSingleton(authOptions);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddHostedService<DemoUserSeeder>();

        // FR-9 (ADR-0013): per-completion-call token/cost accounting, recorded from AgentRunner and
        // AskService via the ITokenUsageRecorder port.
        services.AddSingleton(ModelPricingOptions.FromConfiguration(configuration));
        services.AddSingleton<ITokenUsageRecorder, EfTokenUsageRecorder>();
        services.AddScoped<ITokenUsageQueryService, EfTokenUsageQueryService>();

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

        // Knowledge-corpus ingestion pipeline (ADR-0004): extract -> clean -> chunk -> embed -> index.
        services.AddSingleton<PdfKnowledgeExtractor>();
        services.AddSingleton<DocxKnowledgeExtractor>();
        services.AddSingleton<IDocumentExtractor, CompositeDocumentExtractor>();
        services.AddSingleton<KnowledgeChunker>();
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        // Hybrid retrieval (FR-2, ADR-0005): dense (Qdrant, above) + keyword (BM25 over chunk rows
        // persisted in MSSQL), fused with Reciprocal Rank Fusion.
        services.AddSingleton<Bm25Scorer>();
        services.AddScoped<IKeywordSearchIndex, EfCoreKeywordSearchIndex>();
        services.AddScoped<HybridRetrievalService>();
        services.AddScoped<AskService>();

        services.AddScoped<KnowledgeIngestionService>();

        // T6's OCR pipeline: separate from knowledge-corpus ingestion above (ADR-0004 -- claim
        // paperwork is case data, never routed through that pipeline or searched).
        var ocrOptions = configuration.GetSection(OcrOptions.SectionName).Get<OcrOptions>() ?? new OcrOptions();
        services.AddSingleton(ocrOptions);
        services.AddSingleton<IPdfRasterizer, PdftoppmPdfRasterizer>();
        services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
        services.AddScoped<IScannedDocumentRepository, ScannedDocumentRepository>();
        services.AddScoped<OcrIngestionService>();

        // Deterministic payout tools (D2's non-negotiable control): each is registered both by its
        // concrete type (for direct use) and as IToolExecutor (so an orchestrator can resolve
        // the full set and dispatch by ToolDefinition.Name — see ADR-0006).
        services.AddSingleton<StandardPayoutToolExecutor>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<StandardPayoutToolExecutor>());
        services.AddSingleton<TotalLossDeterminationToolExecutor>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<TotalLossDeterminationToolExecutor>());
        services.AddSingleton<TotalLossSettlementToolExecutor>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<TotalLossSettlementToolExecutor>());
        services.AddSingleton<GapCoverageToolExecutor>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<GapCoverageToolExecutor>());

        // Case-data lookup tools (Coverage Matcher / Anomaly Analyst) — scoped, not singleton,
        // since they depend on the scoped DbContext through their repositories.
        services.AddScoped<LookupDeclarationsToolExecutor>();
        services.AddScoped<IToolExecutor>(sp => sp.GetRequiredService<LookupDeclarationsToolExecutor>());
        services.AddScoped<LookupClaimHistoryToolExecutor>();
        services.AddScoped<IToolExecutor>(sp => sp.GetRequiredService<LookupClaimHistoryToolExecutor>());

        // The write/side-effecting tool (FR-4) — also scoped, for the same reason as the case-data
        // lookup tools above.
        services.AddScoped<FinalizeAdjudicationDecisionToolExecutor>();
        services.AddScoped<IToolExecutor>(sp => sp.GetRequiredService<FinalizeAdjudicationDecisionToolExecutor>());

        // Remaining agent tools: version resolution and knowledge-base search (shared across every
        // agent) depend on scoped repositories/services, so these are scoped too. The damage/value
        // ratio check is pure computation, so it's singleton like the payout tools.
        services.AddScoped<ResolvePolicyVersionToolExecutor>();
        services.AddScoped<IToolExecutor>(sp => sp.GetRequiredService<ResolvePolicyVersionToolExecutor>());
        services.AddScoped<SearchKnowledgeBaseToolExecutor>();
        services.AddScoped<IToolExecutor>(sp => sp.GetRequiredService<SearchKnowledgeBaseToolExecutor>());
        services.AddSingleton<CheckDamageValueRatioToolExecutor>();
        services.AddSingleton<IToolExecutor>(sp => sp.GetRequiredService<CheckDamageValueRatioToolExecutor>());

        // Prompts as versioned files (CLAUDE.md), not string literals — see prompts/*.md.
        var promptOptions = configuration.GetSection(PromptOptions.SectionName).Get<PromptOptions>() ?? new PromptOptions();
        services.AddSingleton(promptOptions);
        services.AddSingleton<IPromptRepository, FilePromptRepository>();

        // The multi-agent workflow (FR-4/FR-5, ADR-0009): four agents, each restricted to the tool
        // set its own prompt/role needs, sharing one AgentRunner (the tool-calling loop) and one
        // orchestrator (the fixed pipeline/state-machine driving AdjudicationCase through them).
        services.AddScoped<AgentRunner>();
        services.AddScoped<CoverageMatcherAgent>();
        services.AddScoped<AnomalyAnalystAgent>();
        services.AddScoped<ExclusionAnalystAgent>();
        services.AddScoped<AdjudicationDrafterAgent>();
        services.AddScoped<AdjudicationOrchestrator>();

        // T6's document-out half (ADR-0011): stateless, so singleton like the other pure-compute
        // pieces in this file.
        services.AddSingleton<IAdjudicationMemoGenerator, AdjudicationMemoGenerator>();
        services.AddScoped<AdjudicationMemoService>();

        return services;
    }
}
