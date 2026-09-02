# ADR-0003: Provider abstraction — Semantic Kernel behind a fixed OpenAI→Ollama fallback chain

**Status**: Accepted
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
