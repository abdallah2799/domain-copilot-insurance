# System Design Document — Domain Copilot

**Status**: living document. Part A is the target this project would build toward with real budget and time; Part B is what actually exists today, with an honest gap table connecting the two — a well-documented cut costs far less, in this project's own grading rubric, than a silently missing one.

## Part A — Unconstrained target architecture

This is the design a funded, production-bound version of Domain Copilot would use — not a wish list disconnected from the domain, but the same D2/T6 problem (grounded claims adjudication with a mandatory human approval gate) scaled past a single dev machine and a 12-day build window.

### A.1 Compute & scaling

- **API**: containerized ASP.NET Core, deployed to a managed container platform (Azure Container Apps or AKS) with horizontal autoscaling on CPU/request-queue depth. Adjudication runs move off the current in-process background `Task` (ADR-0009's own honest limitation) onto a real work queue (Azure Service Bus or SQS) consumed by a separate worker pool — so a run survives an API instance recycling mid-pipeline, and the API tier scales independently of adjudication throughput.
- **Angular SPA**: static build served from a CDN/edge (Azure Static Web Apps or CloudFront+S3), not served by the API process.
- **Multi-instance safety**: the orchestrator's per-run state already lives entirely in `AdjudicationCase` (MSSQL), not in-process memory, so this move is a deployment change, not a redesign — the state machine (ADR-0008) was deliberately built to make this possible.

### A.2 Data stores

- **Vector store**: a managed vector DB (Azure AI Search's vector index, or Pinecone/Qdrant Cloud) instead of a self-hosted Qdrant container — removes an operational burden (backup, upgrade, capacity planning) this project's own Qdrant container currently carries informally.
- **Relational store**: Azure SQL Database (managed, automated backups/point-in-time restore, geo-replication) instead of a single MSSQL container with a Docker volume.
- **Secrets**: a managed secrets manager (Azure Key Vault or AWS Secrets Manager) issuing short-lived credentials to the API at startup, instead of `.env`/environment variables holding long-lived values (the JWT signing key, provider API keys, the MSSQL SA password) — this project's `.env` approach is explicitly a dev-only convenience (gitignored, never committed), not a claim that it's how a real deployment should hold secrets.

### A.3 Identity & access

- A real identity provider (Azure AD B2C, Auth0, or a first-party user store with self-service *provisioning*, not self-service *role selection*) replacing the two seeded demo accounts (ADR-0012) — an admin-driven onboarding flow, MFA for the Adjuster role specifically (since it is the only role that can finalize a payout decision), and token revocation (a real deployment cannot leave a compromised JWT valid until its natural expiry, which this project's own ADR-0012 names as an accepted gap).

### A.4 Observability & cost control

- The current per-call token/cost accounting (FR-9, ADR-0013) scales as-is conceptually, but a production deployment would add: budget alerts (a Slack/PagerDuty webhook when daily estimated cost crosses a threshold), the OTel traces exported to a durable backend (Azure Monitor / Honeycomb / Grafana Tempo) instead of the self-hosted Aspire Dashboard container (which has no long-term retention and is explicitly a local dev/demo tool per ADR-0013), and real anomaly alerting on latency/error-rate per provider leg (distinguishing "OpenRouter is down" from "Ollama is slow" automatically instead of reading log lines by hand, which is how this project diagnosed both during its own build).

### A.5 Disaster recovery

- RPO target: 15 minutes (managed relational + vector store point-in-time recovery). RTO target: under 1 hour for a full region failover, via a warm standby in a second region for the API tier and geo-replicated data stores. None of this exists today — the current single MSSQL container's `mssql-data` Docker volume is the only persistence, with no backup strategy at all beyond "the volume survives a container restart."

### A.6 Cost model (rough, illustrative)

| Component | Target-architecture estimate (USD/month, light production load) |
|---|---|
| Container Apps (API, autoscaled 1-5 instances) | ~$80–250 |
| Azure SQL Database (S1 tier) | ~$75 |
| Managed vector search | ~$100–300 (usage-dependent) |
| Key Vault | ~$5 |
| LLM completions (hosted, paid tier) | Highly variable — this project's own free-tier OpenRouter usage is the reason ADR-0013's cost accounting exists, to make this line item visible rather than guessed |
| Observability backend | ~$50–150 |
| **Total** | **~$300–800/month**, dominated by LLM spend at real usage volumes, not infrastructure |

This is a rough order-of-magnitude estimate for planning discussion, not a quote — the honest answer to "what would this cost" is "mostly LLM tokens, and that number depends entirely on real usage patterns this project has no data on yet."

## Part B — Implemented MVP

### B.1 What actually exists

A single ASP.NET Core process (run via `dotnet run` directly on a dev machine, no Dockerfile yet for the API itself) talking to three containers (MSSQL, Qdrant, and — once FR-9 landed — the Aspire Dashboard OTel viewer), plus an optional fourth (`ollama`, behind the `local-llm` compose profile) for local LLM fallback. The Angular SPA runs via `ng serve` in dev, with a production build (`ng build`) verified to succeed but not yet deployed anywhere. Every data store is a single container with a Docker named volume — no replication, no managed backup, no autoscaling. Secrets live in a gitignored `.env` file. Adjudication runs execute as a fire-and-forget in-process `Task`, not a durable queue — if the API process restarts mid-run, that run is abandoned (a real, named limitation, not an oversight: see the gap table below).

This is deliberately the floor the brief's own MoSCoW framing describes as sufficient for a 12-day assessment, not an attempt to half-build the Part A target.

### B.2 Gap table

| Target (Part A) | Implemented? | Why deferred | Interim mitigation | Effort/cost to close |
|---|---|---|---|---|
| Managed vector store (Azure AI Search / Qdrant Cloud) | No — self-hosted Qdrant container | Time/cost: a managed tier costs real money with no assessment budget for it, and the self-hosted container is functionally identical for a single-instance demo | Qdrant's own gRPC client already speaks the same protocol a managed Qdrant Cloud tier would use — swapping the connection string/host is the entire migration (ADR-0002's own point: the provider-abstraction discipline extends to the vector store choice too) | ~1 day: provision, migrate connection config, re-run the ingestion pipeline against the new endpoint |
| Managed relational store (Azure SQL) | No — self-hosted MSSQL container | Same time/cost reasoning; a single container is sufficient to prove every EF Core migration/repository pattern works against real SQL Server (which is what the Testcontainers-based integration tests already verify) | EF Core migrations are database-engine-agnostic at the code level; the connection string is the only thing that changes | ~0.5 day: provision Azure SQL, re-point `ConnectionStrings__Default`, re-run migrations |
| Secrets manager (Key Vault/Secrets Manager) | No — `.env` (gitignored) / plain environment variables | No assessment-scale reason to stand up a secrets manager for 2 demo accounts and 3 provider API keys; `.env` is never committed and CI never has real secrets exposed to it | `.env.example` documents every required variable with no real values, and gitleaks scans full history in CI | ~1 day: wire `IConfiguration` to read from Key Vault via `Azure.Extensions.AspNetCore.Configuration.Secrets`, no application-code changes needed since config is already read through `IConfiguration` throughout |
| Message queue + separate worker pool for adjudication runs | No — in-process `Task` (`AdjudicationController.RunPipelineInBackgroundAsync`) | A single-instance API has no multi-instance-safety problem to solve yet, and a queue is real infrastructure with no payoff until horizontal scaling is actually needed | The state machine (`AdjudicationCase`, ADR-0008) already holds 100% of run state in MSSQL, not in-process memory — the eventual migration to a queue-backed worker is additive, not a rewrite | ~2-3 days: a worker process consuming a queue, `StartRun` publishes instead of calling `Task.Run`, `AdjudicationOrchestrator`'s own logic is unchanged |
| Autoscaling / multi-instance API | No — single process | Same reasoning as the queue gap above; they'd need to land together | None needed at current single-user-demo load | Tied to the queue migration above |
| Real identity provider, admin-driven onboarding, MFA, token revocation | No — 2 seeded demo accounts (ADR-0012), no revocation list | FR-8's actual floor is "≥2 genuinely different, server-enforced roles" — met without needing a real IdP; open self-registration would defeat the point of a server-enforced role, so a seeded pair was the honest minimum, not a shortcut around a harder problem | Server-side role/ownership enforcement is real regardless of how the two accounts were provisioned — verified live (401/403/ownership checks all demonstrated against the actual API, not asserted) | ~3-5 days for a real IdP integration + admin console; revocation needs either short-lived tokens + refresh, or a server-side deny-list checked per request |
| Durable OTel backend (Azure Monitor / Tempo) with retention + alerting | No — self-hosted Aspire Dashboard, no retention beyond the container's own memory | ADR-0013 explicitly calls the Aspire Dashboard a "local dev/demo tool," not a production observability backend | Traces are real and inspectable during a live session, which was enough to verify the correlation-id/span-nesting design works | ~1 day: point `OTEL_EXPORTER_OTLP_ENDPOINT` at a managed backend instead — no application code changes, since the OTel SDK wiring is endpoint-agnostic |
| DR/backup strategy, RPO/RTO targets | No — a single Docker volume per data store, no backup job | Out of scope for a demo/assessment environment with no production data to protect | None beyond "the volume survives a container restart" | Tied to the managed-data-store migrations above (Azure SQL/managed vector search both include point-in-time recovery) |
| Full 32-entry live evaluation harness run | Partial — 7-entry live sample run (`docs/EVALUATION.md`) | OpenRouter's free-tier key was rate-limited (429) from this project's own extensive verification activity, and the Ollama fallback measured 5–25+ minutes per non-tool completion call on this dev machine's hardware | The 7 entries were chosen to cover every metric category (hit-rate, groundedness, refusal correctness, injection resistance, version-awareness) with at least one real data point each, not the fastest-to-run ones | A few hours of wall-clock time once OpenRouter's quota resets or on faster/GPU-accelerated hardware — the harness itself needs no code changes, `dotnet run --project tools/DomainCopilot.EvaluationHarness` runs the full set as-is |
| API/Angular containerized and deployed | No — both run directly on the dev host (`dotnet run` / `ng serve`) | Deployment is explicitly optional ("strongly valued," not required) per the brief; time went to the Must-have feature floor first | `docker compose up` still brings up every stateful dependency (MSSQL, Qdrant, optionally Ollama, the OTel viewer) — only the two application processes are undockerized | ~1 day: two Dockerfiles (multi-stage .NET build, `nginx`-served Angular static build), add both as compose services |

### B.3 Provider-swap acceptance test

The one Part-A-relevant claim this project can demonstrate live rather than argue abstractly: swapping the primary completion provider is a configuration change only. `ICompletionService`/`IEmbeddingService` (ADR-0003) are the only surface `Application` code depends on; `FallbackCompletionService`/`FallbackEmbeddingService` compose two concrete adapters behind the same interface with zero knowledge of which SDKs they wrap. Disabling `OpenRouter__ApiKey` and re-running any ask/adjudication call forces the Ollama leg with no code change — this was, in fact, exercised for real multiple times this session (not hypothetically) whenever OpenRouter's free-tier quota was exhausted, and every fallback both succeeded functionally and was logged (`FallbackCompletionService[0] Primary completion provider OpenRouter failed, falling back to Ollama`) rather than silently swallowed.
