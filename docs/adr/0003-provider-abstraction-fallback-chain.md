# ADR-0003: Provider abstraction — Semantic Kernel behind a fixed hosted→local fallback chain

**Status**: Accepted (superseded in part — see Update below)
**Date**: 2026-09-02

## Context

The brief mandates a single interface covering completion, streaming, tool-calling, and embeddings, with ≥2 working implementations (a hosted API and a local/alternative model) selected by configuration, and a documented fallback chain — explicitly so the project never needs a paid subscription to run or demo. The acceptance test is that swapping provider, embedding model, or vector store is a configuration-plus-one-adapter change, never a change to business logic.

## Decision

`Application.Providers` defines `ICompletionService` and `IEmbeddingService` (plus the supporting `ChatMessage`/`CompletionRequest`/`CompletionResult`/`ToolDefinition`/`ToolCall` types) as pure ports with zero SDK dependencies. `Infrastructure.Providers` implements each port twice: `OpenAiCompletionService`/`OpenAiEmbeddingService` (hosted, via Semantic Kernel's OpenAI connector) and `OllamaCompletionService`/`OllamaEmbeddingService` (local, via the *same* SK OpenAI connector pointed at Ollama's OpenAI-compatible endpoint — Ollama needs no separate SDK). Both completion adapters delegate to a shared internal `SemanticKernelCompletionAdapter` so the request/response translation logic (history building, tool-schema mapping, usage extraction) is written once, not duplicated per provider.

The fallback chain is a decorator, `FallbackCompletionService`/`FallbackEmbeddingService`, living in `Application` (not `Infrastructure`) since it's pure composition over the port interfaces with no SDK involved — this makes the fallback behavior itself unit-testable with fakes, with no real network calls. The order is fixed at OpenAI-primary/Ollama-fallback in `Infrastructure.DependencyInjection`, not exposed as a config knob, since there was no requirement calling for a configurable direction and an unused knob would violate the project's own "no speculative config" rule.

Tool declarations are deliberately never auto-invoked by Semantic Kernel (`FunctionChoiceBehavior.Auto(kernelFunctions, autoInvoke: false)`): the model may see and choose a tool, but Semantic Kernel never calls it — the resulting `ToolCall` is handed back to Application, which is where the approval gate and schema validation for side-effecting tools will live (Epic E). This is a direct mitigation for the OWASP LLM "excessive agency" risk, decided now so it isn't retrofitted after tool execution exists.

## Alternatives considered

- **A dedicated Ollama SDK/connector** — Semantic Kernel doesn't ship one, and writing a bespoke HTTP client for Ollama would duplicate everything the OpenAI connector already does, since Ollama's `/v1/chat/completions` endpoint is OpenAI-compatible by design. Rejected in favor of reusing one connector with two different endpoint/credential configurations.
- **Fallback logic inside each Infrastructure adapter** (each provider decides for itself whether to fail over) — would mean every new provider adapter has to reimplement retry/fallback semantics, and the behavior wouldn't be unit-testable without real provider calls. Rejected in favor of a single Application-layer decorator any two providers can be wrapped in.
- **Auto-invoked tool calling** (`FunctionChoiceBehavior.Auto(autoInvoke: true)`) — the more common Semantic Kernel pattern, and less code right now. Rejected because it would let the model execute a side-effecting tool before any approval gate exists, which the brief's "at least one workflow step requires explicit human approval" and "no destructive tool without the approval gate" requirements directly forbid.
- **Configurable fallback direction** — more flexible in the abstract, but nothing in the brief or this project's variant calls for switching which provider is primary at runtime, and unused configuration surface is itself a maintenance liability. Rejected for now; revisit if T3-style cost-based routing becomes relevant.

## Consequences

Easier: adding a third provider (e.g. Azure OpenAI) is one new `Infrastructure` class plus a `DependencyInjection.cs` change; the fallback chain and everything upstream (agents, retrieval) never notices. The fallback decorator's mid-stream-failure handling is covered by unit tests with zero network dependency (`FallbackCompletionServiceTests`), including the specific case of a provider failing after it has already streamed partial content — falling back there would silently duplicate output on the client, so that case propagates the failure instead of retrying.

Harder: token/cost usage extraction (`SemanticKernelCompletionAdapter.ExtractUsage`) reads the OpenAI SDK's usage object by reflection on property names rather than a typed reference, because the exact type differs by SDK version and isn't part of Semantic Kernel's own public contract — this is a known soft spot to revisit once Epic H (observability) needs precise, non-defensive cost accounting. The embedding services also pin to Semantic Kernel's now-obsolete `ITextEmbeddingGenerationService` rather than the newer `Microsoft.Extensions.AI.IEmbeddingGenerator`, because the migration path wasn't cleanly resolvable in this SK version without more time than this pass had — tracked as a fast-follow, not silently left unmentioned.

## Update — 2026-09-02: OpenRouter replaces direct OpenAI as the primary completion provider

**Context for the change**: OpenAI's API no longer has a perpetual free tier (confirmed directly against current OpenAI pricing at the time of writing), which conflicts with the brief's "no paid subscription required" design goal and this project's remaining budget. OpenRouter exposes an OpenAI-compatible endpoint over many underlying models, including models still genuinely free to call (verified live against OpenRouter's `/api/v1/models` endpoint, not assumed from training data, since the free-model catalog changes over time) — `nvidia/nemotron-3.5-lightning:free` was selected as the default: 1M token context, explicit `tools`/`tool_choice` support (verified via the same models endpoint), and described by OpenRouter for "high-throughput agentic workloads," which matches this project's multi-agent use case.

**Decision**: add `OpenRouterCompletionService`, built on the exact same pattern as `OllamaCompletionService` (Semantic Kernel's OpenAI connector against a different endpoint/key — this is precisely the "swap provider = config + one adapter" acceptance test the original ADR designed for, now exercised for real). Rebalance the two fallback chains:

- **Completions**: OpenRouter (hosted, primary) → Ollama (local, fallback). OpenRouter's free tier is rate-limited (20 requests/minute, 50/day without any credit purchase, rising to 1,000/day with $10+ lifetime credits, per OpenRouter's published limits) — this makes the Ollama fallback leg materially more important than it was when OpenAI was primary, not just a formality for the acceptance test.
- **Embeddings**: Ollama (local, primary) → OpenAI (hosted, fallback). OpenRouter has no embeddings endpoint (confirmed against its docs), so it isn't part of this chain. Ollama is primary here — not just the fallback, as for completions — because embeddings are cheap to run entirely locally and there's no free-tier request budget to conserve by preferring a hosted call for them. `OpenAiCompletionService` (the direct-OpenAI completion adapter) remains in the codebase but is no longer wired into the default `ICompletionService` chain; it's still available as a documented drop-in if OpenRouter access becomes unavailable.

**Consequences**: the free-tier rate limit means development and the evaluation harness should run primarily against the Ollama leg locally, reserving OpenRouter calls for final verification and demo recording rather than iterative testing — this is an operational note for `docs/EVALUATION.md` and the README's setup instructions once written, not a code change. Nothing about the port interfaces, the fallback decorator, or the tool-declaration approach changed; only which concrete provider occupies which position in each chain.

## Update — 2026-09-04: provider Kernel construction made lazy (real bug found via real usage)

**What broke**: the first real attempt to use `IEmbeddingService` end-to-end (building the knowledge-ingestion pipeline, ADR-0004) crashed at DI resolution with `ArgumentException: apiKey` — from `OpenAiEmbeddingService`, the embeddings *fallback* leg, even though the primary (Ollama) leg was configured correctly and no code path had reached OpenAI at all. Cause: every concrete provider class built its Semantic Kernel `Kernel` (and Semantic Kernel validates the API key immediately when a `Kernel` is built) inside its constructor, and `FallbackEmbeddingService`'s DI registration resolves both the primary and fallback concrete services eagerly to construct the decorator. With no OpenAI key configured — the expected, supported case for this project — the unused fallback leg's constructor ran anyway and crashed the entire `IEmbeddingService` resolution, taking down anything that depended on it, including the primary leg that would have worked fine on its own. Every earlier verification of this session (DI resolution succeeding, `/health/ready` returning `Healthy`) had passed because none of them actually called `EmbedAsync`/`CompleteAsync` — health checks only ping MSSQL and Qdrant. This is exactly why the ingestion pipeline was verified with a real, live run against the real corpus (see ADR-0004's Verification section) rather than stopping at "it compiles and resolves."

**Fix**: `SemanticKernelCompletionAdapter` and both embedding services now take a `Kernel`-building factory/`Lazy<T>` instead of a built `Kernel`, deferring construction (and therefore API-key validation) to first actual use. An unconfigured fallback leg now only fails if a request actually reaches it — which is the correct behavior for a fallback that's expected to sit unused in the common case.

**Why this matters beyond the immediate fix**: this is a general lesson about the fallback-chain pattern this ADR established — DI eagerly resolving both legs of a decorator is a natural, easy-to-miss way to defeat the entire point of "unused fallback shouldn't need to be configured." Applied to all five provider classes for consistency, not just the one that happened to crash first.

## Update (2026-09-06): Groq added as a third completion leg

**What changed.** The completion chain gained Groq, built by nesting the existing two-leg `FallbackCompletionService` rather than generalising it, since it already composes over `ICompletionService` and knows nothing about providers. (Ordering was revised the next day — see the update below.)

**Why.** OpenRouter's free tier allows **50 model requests per day**. A single four-agent adjudication run costs up to ~30 of them (Coverage Matcher 8 iterations, Anomaly Analyst 12, Exclusion Analyst 6, plus the Drafter and any graceful-degrade call). That is roughly two runs a day, and this project hit the wall for real: a day of debugging exhausted the budget mid-afternoon and left the workflow untestable, with the local leg too slow to substitute (`llama3.1` measured at ~10 minutes per completion call, blowing the 20-minute per-stage timeout before finishing stage 1 of 4).

Groq serves open-weight models on its own inference hardware behind an OpenAI-compatible endpoint, with a materially more generous free tier and much lower per-call latency — which matters disproportionately here, because each agent stage is several sequential tool-calling round trips, not one call.

**OpenRouter was kept, not replaced.** It is a genuinely different vendor, so the chain still survives one hosted provider being rate-limited or down. Dropping it would have traded a real resilience property for tidiness.

**What this cost, and why that is the point.** Adding a third provider required one adapter (`GroqCompletionService`), one options class, and three lines of DI wiring. No agent, prompt, orchestrator, tool, or Application/Domain type changed — those layers never learned a new provider exists. This is the brief's provider-swap acceptance test demonstrated under real pressure rather than asserted in a document: the swap happened because a provider limit genuinely blocked the work, and it was a configuration-plus-one-adapter change exactly as this ADR claimed it would be.

**Caveat worth stating.** Groq's model list changes over time and not every model supports tool calling, which every agent here depends on. The model is configuration (`Providers__Groq__Model`), and `.env.example` points at Groq's own model documentation rather than pinning a name this repository would have to chase.


## Update (2026-09-07): OpenRouter leads, Groq second

**What changed.** The chain is now OpenRouter → Groq → Ollama.

**Why.** Measuring a real run made the trade concrete. The two hosted legs are limited in opposite ways:

| | Requests/day | Tokens/minute | Effect on one full run |
|---|---|---|---|
| OpenRouter (free) | ~50 | no tight cap | finishes at conversational speed |
| Groq (free) | 1000 | 8,000 | minutes spent waiting for the bucket |

A four-agent run measured **16 calls / 60,315 tokens, of which 88% were prompt tokens** — every tool-calling round trip re-sends the system prompt, the tool schemas, and the whole accumulated conversation. Against Groq's 8,000 tokens/minute that is ~7.5 minutes of waiting versus roughly 16 seconds of actual inference; the observed gap between Drafter calls was a near-constant 26 seconds.

So Groq's generous daily budget is unusable at speed for *this* workload, while OpenRouter's tight daily budget is perfectly usable a few times a day. A run that finishes in seconds and can be done two or three times a day is worth more than one that always works and takes ten minutes — particularly for demonstrating the system to a person. Putting Groq second means exhausting OpenRouter's day degrades the system to *slow*, not to *broken*.

**Only Groq is wrapped in the rate-limit waiter.** Its 429 is a per-minute token bucket that genuinely refills, so waiting is the correct response. OpenRouter's 429 is a daily quota, where waiting 25 seconds accomplishes nothing and merely delays the fail-over — the same status code meaning two different things, which is why `CompletionProviderException.IsRateLimited` exists but the decision of whether waiting helps is made per-leg at composition time rather than inside the exception.

**Cost of the reorder:** moving two arguments in one DI expression. Nothing else in the system knows the order changed.
