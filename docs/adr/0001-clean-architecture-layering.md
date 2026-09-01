# ADR-0001: Clean Architecture over Hexagonal/Onion/Vertical Slice

**Status**: Accepted
**Date**: 2026-09-01

## Context

The brief mandates that domain and application layers must not depend on any LLM SDK, vector-store SDK, or web framework, and that swapping the LLM provider, embedding model, or vector store must require configuration plus one adapter, not changes to business logic. Several architectural styles satisfy a ports-and-adapters separation: Clean Architecture, Hexagonal, Onion, and Vertical Slice.

## Decision

Use Clean Architecture with four projects and a strict inward dependency rule: `Domain` (entities, value objects, domain errors, zero external dependencies) ← `Application` (use cases, orchestrator/agent contracts, port interfaces such as `ICompletionService`, `IEmbeddingService`, `IVectorStore`) ← `Infrastructure` (Semantic Kernel, Qdrant, EF Core/MSSQL, OCR, and document-generation adapters implementing those ports) ← `Api` (composition root: controllers, SSE streaming, authn/z, OpenAPI). Dependencies only ever point inward; `Api` is the only project allowed to reference `Infrastructure`.

## Alternatives considered

- **Hexagonal (Ports & Adapters), unstructured** — conceptually identical to what we're doing, but without a named project-per-layer convention it's easy to let a controller reach directly into an adapter. The named four-project Clean Architecture split makes the boundary a compiler-enforced project reference, not a convention someone can forget.
- **Vertical Slice Architecture** — better for teams with many independent features that rarely share cross-cutting rules, and reduces ceremony for CRUD-shaped systems. Rejected here because the D2 domain has strong cross-slice invariants (the deterministic payout calculation, version-aware retrieval, the approval gate) that are easier to enforce and unit-test as shared Application/Domain services than to duplicate or coordinate across per-feature slices.
- **Onion Architecture** — nearly identical to Clean Architecture in intent; Clean Architecture's explicit "Api depends on Infrastructure only at the composition root" framing maps most directly onto ASP.NET Core's `Program.cs` DI registration model, so it was chosen as the more precisely documented variant for this codebase.

## Consequences

Easier: the "swap provider = config + one adapter" acceptance test becomes mechanical — a new class in `Infrastructure` implementing an existing `Application` interface, registered in `Program.cs`. Domain/Application unit tests never need a real LLM, vector store, or database. Harder: more projects and explicit interfaces to maintain than a single-project CRUD app would need; every new capability that touches an external system requires deliberately deciding which layer it belongs in, which adds a small amount of upfront design cost per feature.
