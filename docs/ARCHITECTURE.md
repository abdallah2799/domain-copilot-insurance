# Architecture — Domain Copilot

**Status**: living document, updated as each epic lands. Diagram source lives in this file (Mermaid, rendered natively on GitHub) — there is no separate rendered-image pipeline to keep in sync.

## 1. C4 Level 1 — System Context

```mermaid
C4Context
title Domain Copilot — System Context

Person(analyst, "Analyst", "Ingests documents, asks grounded questions, starts adjudication runs")
Person(adjuster, "Adjuster", "Everything an Analyst can do, plus the only role that may approve/reject/edit-and-approve a decision")

System(copilot, "Domain Copilot", "Agentic RAG platform for insurance claims adjudication (D2/T6)")

System_Ext(openrouter, "OpenRouter", "Hosted LLM completions (free-tier model) — primary")
System_Ext(ollama, "Ollama", "Local LLM completions + embeddings — fallback for completions, primary for embeddings")
System_Ext(openai, "OpenAI", "Hosted embeddings — fallback only, and completions if ever configured")
System_Ext(otel, "OTel viewer", "Self-hosted .NET Aspire Dashboard — traces/logs/metrics, not a real external system")

Rel(analyst, copilot, "Uses", "HTTPS")
Rel(adjuster, copilot, "Uses, approves decisions", "HTTPS")
Rel(copilot, openrouter, "Completion requests", "HTTPS")
Rel(copilot, ollama, "Completion + embedding requests", "HTTP (local)")
Rel(copilot, openai, "Embedding requests (fallback)", "HTTPS")
Rel(copilot, otel, "OTLP traces", "gRPC")
```

Everything the system talks to outside its own boundary is a commodity LLM/embedding provider (swappable per ADR-0003) or a local observability viewer — there is no third-party insurance system, payment gateway, or identity provider integration, deliberately, since none is in scope for D2/T6.

## 2. C4 Level 2 — Containers

```mermaid
C4Container
title Domain Copilot — Containers

Person(user, "Analyst / Adjuster")

Container_Boundary(copilot, "Domain Copilot") {
  Container(spa, "Angular SPA", "Angular 20", "Ingest, ask+citations, adjudication runs, approval queue, observability view, login")
  Container(api, "API", "ASP.NET Core 10 / .NET 10", "Controllers, orchestrator, agents, tools, provider adapters (the only container with business logic)")
  ContainerDb(mssql, "MSSQL", "SQL Server 2022 (container)", "Documents, chunks, case data, adjudication cases, scanned documents, users, token usage")
  ContainerDb(qdrant, "Qdrant", "Vector DB (container)", "Dense embeddings for the knowledge corpus, filterable by version/date/category")
  Container(ollama, "Ollama", "Local LLM runtime (container, local-llm profile)", "llama3.1 completions + nomic-embed-text embeddings")
  Container(otel, "Aspire Dashboard", "OTel viewer (container)", "Accepts OTLP directly, no separate collector")
}

System_Ext(openrouter, "OpenRouter API")
System_Ext(openai, "OpenAI API")

Rel(user, spa, "HTTPS")
Rel(spa, api, "JSON over HTTPS, JWT bearer, SSE for streaming", "REST")
Rel(api, mssql, "EF Core / TDS")
Rel(api, qdrant, "gRPC")
Rel(api, ollama, "HTTP")
Rel(api, openrouter, "HTTPS")
Rel(api, openai, "HTTPS")
Rel(api, otel, "OTLP/gRPC")
```

The API is a single ASP.NET Core process — no separate worker/queue container. Background adjudication runs execute as a fire-and-forget `Task` inside the same process against its own DI scope (`AdjudicationController.RunPipelineInBackgroundAsync`), not a separate hosted service or message-queue consumer; see `docs/SYSTEM-DESIGN.md`'s gap table for what a horizontally-scaled version of this would need instead. `api`/`angular-app` have no Dockerfile yet (both run via `dotnet run`/`ng serve` directly on the host in this dev setup) — see the gap table.

## 3. C4 Level 3 — Components (inside the API)

```mermaid
C4Component
title Domain Copilot API — Components

Container_Boundary(api, "DomainCopilot.Api") {
  Component(authCtrl, "AuthController", "Controller", "POST /api/auth/login — the one AllowAnonymous endpoint")
  Component(ingestCtrl, "IngestionController", "Controller", "Knowledge-corpus ingest, document listing")
  Component(retrievalCtrl, "RetrievalController", "Controller", "ask, ask/stream (SSE), search")
  Component(caseDataCtrl, "CaseDataController", "Controller", "Loads Declarations/claim-history case data")
  Component(ocrCtrl, "OcrController", "Controller", "Scanned document upload + OCR status")
  Component(adjCtrl, "AdjudicationController", "Controller", "Start/stream/approve/reject/edit-and-approve a run, memo download")
  Component(obsCtrl, "ObservabilityController", "Controller", "Token/cost usage report")
}

Container_Boundary(app, "DomainCopilot.Application") {
  Component(authSvc, "AuthService", "Use case", "Login: verify + issue JWT")
  Component(ingestSvc, "KnowledgeIngestionService", "Use case", "extract -> clean -> chunk -> embed -> index")
  Component(askSvc, "AskService / HybridRetrievalService", "Use case", "Refuse-or-answer with citations; dense+keyword fusion")
  Component(ocrSvc, "OcrIngestionService", "Use case", "Rasterize -> OCR -> confidence -> review routing")
  Component(orchestrator, "AdjudicationOrchestrator", "Pipeline/state-machine", "Drives AdjudicationCase through 4 stages + approval gate")
  Component(agents, "4 Agents + AgentRunner", "Tool-calling loop", "CoverageMatcher, AnomalyAnalyst, ExclusionAnalyst, AdjudicationDrafter")
  Component(tools, "9 IToolExecutor tools", "Gated capabilities", "4 deterministic payout/loss tools, 2 case-data lookups, version resolution, knowledge search, 1 write tool (finalize decision)")
  Component(memoSvc, "AdjudicationMemoService", "Use case", "Same stage data -> PDF memo (T6 doc-out)")
  Component(ports, "Ports", "Interfaces", "ICompletionService, IEmbeddingService, IVectorStore, IKeywordSearchIndex, IOcrEngine, IPdfRasterizer, repositories, ITokenUsageRecorder")
}

Container_Boundary(infra, "DomainCopilot.Infrastructure") {
  Component(providers, "Provider adapters", "SDK-specific", "OpenRouter/Ollama/OpenAI via Semantic Kernel; FallbackCompletionService/FallbackEmbeddingService chains")
  Component(efRepos, "EF Core repositories", "MSSQL", "One per aggregate; TokenUsageRecords via its own IDbContextFactory")
  Component(qdrantStore, "QdrantVectorStore", "Qdrant.Client", "Upsert/search with version/date/category filters")
  Component(ocrInfra, "TesseractOcrEngine / PdftoppmPdfRasterizer", "Shelled-out CLIs", "Real per-word confidence via TSV output")
  Component(memoGen, "AdjudicationMemoGenerator", "QuestPDF", "Renders the 5-section memo PDF")
  Component(otelWiring, "OTel + DomainCopilotActivitySource wiring", "OpenTelemetry SDK", "Composition-root only; Application only ever touches the BCL ActivitySource")
}

Rel(adjCtrl, orchestrator, "starts/streams/finalizes")
Rel(orchestrator, agents, "drives, in fixed order")
Rel(agents, tools, "restricted per-agent allow-list")
Rel(retrievalCtrl, askSvc, "")
Rel(askSvc, ports, "ICompletionService, IVectorStore, IKeywordSearchIndex")
Rel(ports, providers, "implemented by")
Rel(ports, efRepos, "implemented by")
Rel(ports, qdrantStore, "implemented by")
Rel(ocrCtrl, ocrSvc, "")
Rel(ocrSvc, ocrInfra, "IOcrEngine, IPdfRasterizer")
Rel(adjCtrl, memoSvc, "")
Rel(memoSvc, memoGen, "IAdjudicationMemoGenerator")
```

Every arrow crossing the `Application` boundary from `Infrastructure` runs through an interface defined in `Application` — this is the enforced boundary ADR-0001 describes, checked mechanically by the project-reference graph (`Domain`/`Application` have zero references to Semantic Kernel, `Qdrant.Client`, EF Core provider packages, or ASP.NET Core).

## 4. Sequence diagram — the full agentic workflow (FR-4/FR-5/FR-6)

```mermaid
sequenceDiagram
    actor Analyst
    participant API as AdjudicationController
    participant Orch as AdjudicationOrchestrator
    participant CM as CoverageMatcherAgent
    participant AA as AnomalyAnalystAgent
    participant EA as ExclusionAnalystAgent
    participant AD as AdjudicationDrafterAgent
    participant Tools as IToolExecutor tools
    participant LLM as ICompletionService (OpenRouter->Ollama)
    actor Adjuster

    Analyst->>API: POST /api/adjudication/runs
    API->>Orch: StartCaseAsync (creates AdjudicationCase, status=Pending)
    API-->>Analyst: 200 OK, run id (pipeline runs in background)
    Analyst->>API: GET /api/adjudication/runs/{id}/stream (SSE)

    Orch->>Orch: BeginCoverageMatching()
    Orch->>CM: RunAsync(claim, policy, dateOfLoss, lossType)
    loop tool-calling loop (AgentRunner, max-iteration breaker)
        CM->>LLM: complete(system+user+tools)
        LLM-->>CM: tool call e.g. resolve_policy_version
        CM->>Tools: ResolvePolicyVersionToolExecutor
        Tools-->>CM: form version + effective date
        CM->>Tools: search_knowledge_base
        Tools-->>CM: cited chunks
    end
    CM-->>Orch: CoverageMatchResult (typed, cited)
    Orch->>API: (SSE) status=DetectingAnomalies
    API-->>Analyst: live update

    Orch->>AA: RunAsync(..., coverageMatch)
    AA->>Tools: lookup_declarations, lookup_claim_history, check_damage_value_ratio
    AA-->>Orch: AnomalyFindings
    Orch->>EA: RunAsync(coverageMatch, anomalyFindings)
    EA->>Tools: search_knowledge_base
    EA-->>Orch: ExclusionAnalysisResult
    Orch->>AD: RunAsync(coverageMatch, anomalyFindings, exclusionAnalysis)
    AD->>Tools: standard_payout / total_loss_settlement / gap_coverage (deterministic, never LLM math)
    AD-->>Orch: Recommendation (cites the tool's own output verbatim)

    Orch->>Orch: RecordRecommendation() -> status=AwaitingApproval
    API-->>Analyst: (SSE) final update, stream closes (nothing left to report until a human acts)

    Adjuster->>API: GET /api/adjudication/runs/{id}
    Adjuster->>API: POST /api/adjudication/runs/{id}/approve
    API->>Tools: FinalizeAdjudicationDecisionToolExecutor (the one write/side-effecting tool)
    Tools->>Orch: AdjudicationCase.Approve(actor=JWT username)
    API-->>Adjuster: 200 OK, status=Approved
    Adjuster->>API: GET /api/adjudication/runs/{id}/memo
    API-->>Adjuster: generated PDF (T6 doc-out, same stage data)
```

Any per-stage failure (timeout, retry-exhausted completion call, non-conforming JSON) degrades to a plain-RAG summary and marks the case `Failed` rather than leaving it stuck — not shown above for clarity; see ADR-0009's "graceful degrade" section.

## 5. Data-flow diagram — trust boundaries and what the LLM provider sees

```mermaid
flowchart LR
    subgraph Untrusted["Untrusted input"]
        Browser["Browser / API caller"]
        ScannedDoc["Uploaded scanned document"]
    end

    subgraph Boundary1["Trust boundary: authentication + authorization"]
        API["ASP.NET Core API<br/>(JWT validation, role checks, object-ownership checks)"]
    end

    subgraph Internal["Trusted internal data"]
        DB[("MSSQL<br/>case data, users, adjudication cases")]
        Vec[("Qdrant<br/>knowledge-corpus embeddings")]
    end

    subgraph Boundary2["Trust boundary: what leaves the process to a third party"]
        Prompt["Prompt construction<br/>(system prompt + retrieved chunks + user question only)"]
    end

    subgraph External["External LLM/embedding providers<br/>(treated as untrusted output, never as authoritative instructions)"]
        LLM["OpenRouter / Ollama / OpenAI"]
    end

    Browser -->|JWT + JSON| API
    ScannedDoc -->|multipart upload| API
    API -->|EF Core, parameterized| DB
    API -->|gRPC, filtered search| Vec
    DB -->|retrieved chunks, case facts| Prompt
    Vec -->|retrieved chunks| Prompt
    Browser -.->|question text: own slot, never re-labeled as policy content| Prompt
    ScannedDoc -.->|OCR'd text: same untrusted-question slot| Prompt
    Prompt -->|system+user messages, tool schemas| LLM
    LLM -->|completion text, tool-call requests only| API
    API -->|validated tool args, never raw-executed| Internal
```

What the LLM provider never sees: the JWT signing key, database connection strings, any user's password hash, other users' adjudication cases, or the raw contents of `.env`. What it does see: the corpus text already indexed in Qdrant/MSSQL (synthetic, no real personal data — BR-04), the current user's own question/OCR text, and tool-call results it explicitly requested. The user's own question and any OCR'd document text are placed in their own prompt slot, never concatenated into the "retrieved passages" section a real policy chunk occupies — this is the structural reason a prompt-injection attempt embedded in either can't make itself indistinguishable from real corpus content (see `docs/SECURITY.md`, LLM01).

## 6. Entity-relationship diagram

```mermaid
erDiagram
    Document ||--o{ ChunkRecord : "chunked into"
    Document {
        guid Id PK
        string SourceId
        string Title
        string Category
        string FormVersion
        date EffectiveDate
        string Status
    }
    ChunkRecord {
        guid Id PK
        guid DocumentId FK
        int ChunkIndex
        string SectionTitle
        int PageNumber
        string Text
    }
    PolicyDeclaration {
        guid Id PK
        string PolicyNumber
        string FormVersion
        date EffectiveDate
    }
    ClaimHistoryRecord {
        guid Id PK
        string PolicyNumber
        string ClaimNumber
        string LossType
        date DateOfLoss
    }
    AdjudicationCase {
        guid Id PK
        string ClaimNumber
        string PolicyNumber
        date DateOfLoss
        string Status
        string CreatedByUsername
        string ApprovedBy
    }
    ScannedDocument {
        guid Id PK
        string ClaimNumber
        string Status
        float OverallConfidencePercent
    }
    User {
        guid Id PK
        string Username
        string PasswordHash
        string Role
    }
    TokenUsageRecord {
        guid Id PK
        string CorrelationId
        string AgentName
        string ProviderName
        int PromptTokens
        int CompletionTokens
        decimal EstimatedCostUsd
    }
```

`PolicyDeclaration`/`ClaimHistoryRecord`/`AdjudicationCase`/`ScannedDocument`/`User`/`TokenUsageRecord` have no foreign keys to each other or to `Document` — case data is looked up by exact key (policy/claim number), never joined or searched (ADR-0004); an `AdjudicationCase` records citations as plain strings inside its stage JSON blobs, not FK references, since a citation identifies a real chunk by title/section/page text, not a database id a human reading the memo needs.

## 7. Layer-dependency diagram

```mermaid
flowchart TD
    Api["DomainCopilot.Api<br/>(ASP.NET Core, composition root)"] --> Infra["DomainCopilot.Infrastructure<br/>(EF Core, Qdrant.Client, Semantic Kernel, Tesseract, QuestPDF)"]
    Infra --> App["DomainCopilot.Application<br/>(ports + use cases, zero SDK deps)"]
    App --> Domain["DomainCopilot.Domain<br/>(entities, zero external deps)"]
    Angular["ui/angular-app<br/>(Angular SPA)"] -.->|HTTP/JSON, JWT, SSE only| Api
    Tools["tools/DomainCopilot.EvaluationHarness<br/>(black-box console client)"] -.->|HTTP only, no project reference| Api
```

Arrows point inward only, mechanically enforced by the `.csproj` `ProjectReference` graph (ADR-0001) — `dotnet list package` on `Domain`/`Application` shows zero LLM/vector-store/web-framework packages, checked as part of this project's own review discipline, not just asserted.

## 8. Architecture Decision Records

| ADR | Decision |
|---|---|
| [0001](adr/0001-clean-architecture-layering.md) | Clean Architecture over Hexagonal/Vertical Slice, with the inward-dependency rule mechanically enforced |
| [0002](adr/0002-vector-store-qdrant.md) | Qdrant as the vector store |
| [0003](adr/0003-provider-abstraction-fallback-chain.md) | `ICompletionService`/`IEmbeddingService` ports; OpenRouter->Ollama completions, Ollama->OpenAI embeddings |
| [0004](adr/0004-chunking-and-knowledge-vs-case-data-split.md) | Knowledge corpus (chunked/embedded/searched) vs. case data (exact-key lookup only) split |
| [0005](adr/0005-hybrid-retrieval-and-version-aware-filtering.md) | Dense+keyword hybrid retrieval (RRF fusion), version/date-aware metadata filtering |
| [0006](adr/0006-deterministic-payout-calculation.md) | All payout/limit/deductible math is plain, unit-tested C# — never LLM-computed |
| [0007](adr/0007-case-data-foundation-for-agent-tools.md) | Structured case-data tables for the lookup tools, sourced from the corpus generator directly |
| [0008](adr/0008-approval-gate-state-machine.md) | `AdjudicationCase`'s own state machine enforces the human approval gate structurally |
| [0009](adr/0009-multi-agent-orchestrator.md) | Fixed pipeline/state-machine orchestrator (not an open-ended planner) driving 4 agents |
| [0010](adr/0010-ocr-confidence-and-review-routing.md) | T6 OCR: real per-page confidence, `NeedsReview` routing below threshold |
| [0011](adr/0011-generated-adjudication-memo.md) | T6 doc-out: QuestPDF-generated memo from the same stage data as the chat answer |
| [0012](adr/0012-authentication-and-object-level-authorization.md) | JWT auth, two roles, object-ownership checks on adjudication runs |
| [0013](adr/0013-observability-correlation-tracing-and-cost-accounting.md) | Correlation IDs via ASP.NET Core's own Activity, OpenTelemetry tracing, persisted token/cost accounting |

## 9. What's deliberately not shown here

Deployment topology beyond "runs via `docker compose`/`dotnet run` on one host" (no Kubernetes manifests, no multi-region setup) and a full target-state architecture are `docs/SYSTEM-DESIGN.md`'s job, not this document's — this file describes what actually exists and how its pieces fit together, not what a funded, scaled version would look like.
