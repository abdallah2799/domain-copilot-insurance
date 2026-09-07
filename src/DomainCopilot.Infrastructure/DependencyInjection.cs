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
        var groqOptions = configuration.GetSection(GroqOptions.SectionName).Get<GroqOptions>() ?? new GroqOptions();
        var cassetteOptions = configuration.GetSection(CassetteOptions.SectionName).Get<CassetteOptions>() ?? new CassetteOptions();

        services.AddSingleton(openAiOptions);
        services.AddSingleton(ollamaOptions);
        services.AddSingleton(openRouterOptions);
        services.AddSingleton(groqOptions);
        services.AddSingleton(cassetteOptions);

        services.AddSingleton<OpenAiCompletionService>();
        services.AddSingleton<OllamaCompletionService>();
        services.AddSingleton<OpenRouterCompletionService>();
        services.AddSingleton<GroqCompletionService>();
        services.AddSingleton<OpenAiEmbeddingService>();
        services.AddSingleton<OllamaEmbeddingService>();
        services.AddSingleton<ICompletionCassetteStore, FileCompletionCassetteStore>();

        // Completions: OpenRouter (hosted, primary) -> Groq (hosted, second) -> Ollama (local, last).
        // Three legs from the same two-leg FallbackCompletionService by nesting it, since it composes
        // over the port rather than knowing anything about providers.
        //
        // The two hosted legs are limited in opposite ways, and the ordering trades one against the
        // other deliberately. OpenRouter allows only ~50 model requests per day but imposes no tight
        // per-minute token ceiling, so a full four-agent run completes at conversational speed.
        // Groq allows 1000 requests a day but only 8000 tokens per minute, and this project's
        // prompts are large enough (88% of every call is re-sent conversation) that a run spends
        // minutes waiting for the bucket to refill rather than on inference.
        //
        // OpenRouter leads because a run that finishes in seconds and can be done a few times a day
        // beats one that always works but takes ten minutes. Groq behind it means exhausting the
        // daily budget degrades to slow rather than to broken.
        //
        // Only the Groq leg is wrapped in the rate-limit waiter: its 429 is a per-minute bucket that
        // refills, so waiting is right. OpenRouter's is a daily quota, where waiting 25 seconds
        // achieves nothing and simply delays the fail-over.
        //
        // CompletionMode (ADR-0014) then wraps that live chain for the record/replay workflow: one
        // real recorded run replays unlimited times, which is what makes an end-to-end run testable
        // at all against a daily budget a single run can nearly exhaust by itself.
        var completionMode = configuration.GetValue("Providers:CompletionMode", CompletionMode.Live);
        services.AddSingleton<ICompletionService>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FallbackCompletionService>>();

            // Six attempts, not the default three: three gives Groq only ~75 seconds of patience,
            // and when a stage makes several calls in quick succession that is not always enough for
            // an 8000-token minute to refill. Falling through at that point lands on Ollama, where a
            // single call can take ten minutes and blow the orchestrator's per-stage timeout -- so a
            // recoverable pause becomes a failed run. Waiting longer is strictly better than that.
            var groqWithBackoff = new RateLimitAwareCompletionService(
                sp.GetRequiredService<GroqCompletionService>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RateLimitAwareCompletionService>>(),
                maxAttempts: 6);

            var live = new FallbackCompletionService(
                sp.GetRequiredService<OpenRouterCompletionService>(),
                new FallbackCompletionService(
                    groqWithBackoff,
                    sp.GetRequiredService<OllamaCompletionService>(),
                    logger),
                logger);

            return completionMode switch
            {
                CompletionMode.Record => new RecordingCompletionService(live, sp.GetRequiredService<ICompletionCassetteStore>()),
                CompletionMode.Replay => new ReplayCompletionService(sp.GetRequiredService<ICompletionCassetteStore>()),
                _ => live,
            };
        });

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

        // The "upload the claim's own paperwork instead of retyping it" half of starting a run --
        // reuses the same OCR ports as the pipeline above, deliberately without going through it
        // (see ClaimIntakeExtractionService's own doc comment for why).
        services.AddScoped<ClaimIntakeExtractionService>();

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
