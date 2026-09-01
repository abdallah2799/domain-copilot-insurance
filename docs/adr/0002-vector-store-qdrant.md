# ADR-0002: Vector store — Qdrant

**Status**: Accepted
**Date**: 2026-09-01

## Context

The brief requires a relational store plus a vector store with migrations, selected such that swapping it later is config-plus-one-adapter, and requires no paid subscription (free tier or self-hostable). D2's named risk — retrieval must be version/date-aware so an adjuster is never shown guidance from the wrong policy version — means the vector store must support rich, filterable payload metadata (`policyVersion`, `effectiveDate`, `documentId`, `section/clause`), not just similarity search.

## Decision

Use Qdrant, self-hosted via Docker Compose, accessed through the official `Qdrant.Client` .NET SDK, wrapped behind an `Application`-layer `IVectorStore` port so `Infrastructure.Qdrant` is the only project referencing the client SDK.

## Alternatives considered

- **pgvector (inside the MSSQL/Postgres story)** — would reduce the number of moving parts to one database, but this project already commits to MSSQL for relational data per the assigned stack, and pgvector requires Postgres; running vectors as a MSSQL extension isn't a mature option. Rejected to avoid mixing two relational engines.
- **Azure AI Search / a managed vector DB** — capable and would map well to the Part A "target architecture" (see `docs/SYSTEM-DESIGN.md`), but requires a paid tier or Azure-specific credits beyond what's guaranteed free, and would tie retrieval logic to a specific cloud vendor's filter-query syntax. Rejected for the MVP; recorded as the Part A target instead.
- **In-memory / SQLite-based vector search (e.g. a simple cosine-similarity table)** — trivial to stand up but has no native payload filtering, no HNSW-style ANN indexing, and would not demonstrate hybrid retrieval at any realistic scale. Rejected because it would undercut the FR-2 hybrid-retrieval requirement.

## Consequences

Easier: Qdrant's payload filtering maps directly onto the version/date-aware retrieval requirement — a query can request "nearest neighbors where `policyVersion` = X and `effectiveDate` <= claim date" in one call, which is exactly the guard the D2 risk calls for. Free, self-hostable, single Docker image, satisfies "no paid subscription" outright. Harder: it is one more stateful service in `docker-compose.yml` to keep healthy alongside MSSQL, and its hybrid (dense+sparse) query API is still evolving, so the fusion method chosen for FR-2 needs its own ADR once retrieval is implemented (Day 2) rather than being decided here.
