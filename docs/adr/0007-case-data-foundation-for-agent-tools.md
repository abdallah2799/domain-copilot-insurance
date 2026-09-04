# ADR-0007: Structured case-data storage for agent lookup tools, sourced from the generator's own facts

**Status**: Accepted
**Date**: 2026-09-05

## Context

The planned multi-agent workflow (FR-4/FR-5, not yet built) needs two lookup tools with real data behind them: Coverage Matcher's `lookup_declarations` (a policyholder's coverage parts, limits, deductibles, endorsements) and Anomaly Analyst's `lookup_claim_history` (other claims on the same policy, for the 90-day duplicate-claims check). Neither exists yet — ADR-0004 deliberately kept `declarations/` and `claims/` out of the knowledge-corpus pipeline entirely (never chunked, embedded, or searched), which was the right call for *that* problem, but left no relational store for this different one: an agent needs to look up one policyholder's or one claim's exact structured facts by key, not search semantically across many.

## Decision

Two new Domain entities under `DomainCopilot.Domain.CaseData` — `PolicyDeclaration` and `ClaimHistoryRecord` — persisted relationally in MSSQL via their own repositories, loaded from a new structured export the corpus generator produces directly: `case-data.json`, written by `write_case_data()` in `build_corpus.py` straight from `facts.py`'s existing `POLICYHOLDERS`/`CLAIMS` Python dataclasses. The two lookup tools (`LookupDeclarationsToolExecutor`, `LookupClaimHistoryToolExecutor`) wrap these repositories behind the same `IToolExecutor` contract the payout tools already use (ADR-0006).

Loading this data is a separate pipeline from `KnowledgeIngestionService`, deliberately: no extraction, cleaning, chunking, embedding, or vector indexing, because none of it is ever searched. It's a plain, idempotent (by natural key) relational load — `CaseDataLoadingService`, triggered via `POST /api/casedata/load`, mirroring `IngestionController`'s pattern without inheriting its concerns.

## Alternatives considered

- **Parse the generated declarations/claims prose documents (DOCX/scanned PDF) to recover structured facts** — rejected. The generator already holds this data in structured form before it's ever rendered into prose; re-deriving it by parsing the rendered output would be strictly less reliable for the exact same information, and is backwards from how a real insurer's systems work (the Declarations PDF a policyholder receives is a *rendering* of policy admin system data, not the other way around). Parsing prose to recover structure that already exists upstream is manufactured fragility with no compensating benefit.
- **Store Declarations/claim facts in Qdrant alongside the knowledge corpus, filtered out of search results** — rejected for the same reason ADR-0004 rejected embedding case data at all: nothing ever retrieves this by similarity, it's always fetched by an exact key the caller already has (a policy or claim number). A vector store is the wrong tool for an exact-key lookup regardless of which collection it lives in.
- **A single unified case-data table with optional/nullable columns for both policy and claim facts** — rejected in favor of two focused entities. Policies and claims have almost no field overlap (a policy has coverage limits, a claim has a date of loss and a narrative), and a merged shape would mean most rows carry a majority of null columns — the same reasoning ADR-0004 already applied when it rejected a single knowledge-and-case-data pipeline.

## A refactor this decision forced: generalizing `IPayoutToolExecutor` to `IToolExecutor`

Building these two tools exposed a real gap in ADR-0006's original design: `IPayoutToolExecutor.Execute` was synchronous, because none of the four payout calculators do any I/O. `lookup_declarations`/`lookup_claim_history` need a real async database call. Rather than create a second, parallel tool-interface family (one sync, one async) that a future orchestrator would have to special-case when dispatching by tool name, `IPayoutToolExecutor` was renamed `IToolExecutor`, moved from `Adjudication` to `Providers` (alongside `ToolDefinition`/`ToolCall`, which is where a tool-calling *contract* belongs rather than under any one business domain that happens to use it), and its method became `Task<ToolExecutionResult> ExecuteAsync(...)`. All four existing payout executors and their tests were updated to the new async signature — a small, contained migration now, versus a much larger one after an orchestrator existed and had already committed to dispatching against two different tool shapes.

## Consequences

Easier: the corpus and the agent tools' data can never drift apart the way re-parsed prose could — both are generated from the same `facts.py` source in the same build step. `IToolExecutor` now has exactly one shape regardless of whether a given tool happens to need I/O, so a future orchestrator's dispatch-by-name logic doesn't need to know or care which.

Harder: `CaseDataLoadingService`'s idempotency is skip-only (an already-loaded policy/claim number is never updated, only skipped) — correct for this corpus, which doesn't change within a session, but a real system syncing from a live policy admin system would need an update path this doesn't provide. Two lookup tools now depend on a second ingestion-adjacent pipeline (`CaseDataLoadingService` alongside `KnowledgeIngestionService`) that has to be run before the agents that need it — undocumented ordering that will need to be made explicit once the orchestrator (or a startup check) actually depends on it.

## Verification

150 tests total (up from 113): 6 new Domain entity tests, 6 new `CaseDataLoadingService` tests (fakes), 9 new tool-executor tests (fakes, covering missing-argument and malformed-JSON handling consistent with `ToolArguments`' existing discipline), 8 new Testcontainers integration tests against a real MSSQL instance — specifically to prove the `Endorsements` list-as-JSON-column conversion round-trips through real SQL Server (not just an in-memory provider) and that the date-window query for duplicate-claim detection translates correctly to real SQL and excludes out-of-window claims.

Real end-to-end verification surfaced two genuine bugs, both fixed before merging, neither caught by any unit test because both were about the *shape* of real data crossing a real boundary:

1. `case-data.json`'s keys are snake_case (from the Python source), but the deserializing `JsonSerializerOptions` only had `PropertyNameCaseInsensitive` set — which bridges casing, not `effective_date` vs `EffectiveDate` naming convention. The first live load attempt threw `ArgumentNullException` on `DateOnly.Parse(null)`, since every date field silently deserialized to null. Fixed with `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`.
2. The `ClaimLossType` enum was built from a comment in `facts.py`'s `Claim` dataclass (`"Collision" | "Comprehensive" | "Liability"`) that turned out to be stale — the real data includes a fourth value, `"UM/UIM"`, which isn't even a valid C# enum literal. The second live load attempt threw partway through, on the first UM/UIM claim it reached. Fixed by adding `ClaimLossType.UmUim` and replacing the bare `Enum.TryParse` with an explicit string-to-enum mapping that fails loudly on a genuinely unrecognized value rather than assuming the source strings are always valid identifiers.

After both fixes, `POST /api/casedata/load` was run against the live corpus end to end: 18 policy declarations and 18 claim history records loaded, cross-checked with a direct SQL query against the real MSSQL container (loss-type distribution: 9 Collision, 7 Comprehensive, 1 Liability, 1 UmUim — matching the corpus exactly), and the `Endorsements` JSON column confirmed readable as a real JSON array, not an opaque string. A second load call confirmed idempotency (0 loaded, all 36 records skipped).
