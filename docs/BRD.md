# Business Requirements Document — Domain Copilot (Insurance Claims Adjudication)

**Status**: living document, updated as each requirement is implemented. See the traceability matrix at the bottom for current state.

## 1. Context

An insurance organization's adjusters spend significant time cross-referencing policy documents, coverage schedules, and exclusion clauses to adjudicate claims, and risk two specific failure modes: applying an outdated policy version, and trusting an LLM's arithmetic for payout/limit/deductible calculations. Domain Copilot ingests the policy and claims corpus, answers coverage questions with verifiable citations, and runs a three-agent adjudication workflow (coverage matching → exclusion analysis → drafting) that always routes through a human adjuster before any decision is finalized.

## 2. Assigned variant

- **Domain**: D2 — Insurance claims adjudication
- **Twist**: T6 — Document in/out (OCR ingestion with confidence handling; generated DOCX/PDF adjudication memo with citations and tables)
- **Derivation**: stated directly in the assignment invitation email, not derived from National ID.

## 3. Personas

| Persona | Role | Needs |
|---|---|---|
| **Adjuster** | Mandated by the D2 approval gate; the only role that may approve, reject, or edit-and-approve an adjudication decision. | Fast, grounded coverage answers with citations; full visibility into what each agent did and why before signing off; cannot be forced to accept an ungrounded or version-ambiguous recommendation. |
| *(second FR-8 role — TBD)* | Satisfies the "≥2 roles with genuinely different permissions" floor; scope to be defined when auth is implemented (Day 4). | — |

## 4. Objectives (measurable)

| ID | Objective | Measurable acceptance criterion |
|---|---|---|
| OBJ-1 | Every claim answer is grounded or explicitly refused | 0% of golden-set answers make an unsupported claim; refusal rate on out-of-corpus questions matches expected refusals in `docs/EVALUATION.md` |
| OBJ-2 | No payout/limit/deductible figure is ever LLM-computed | 100% of such figures trace to `PayoutCalculationService` unit-tested output, verifiable by code review and contract tests |
| OBJ-3 | No decision reaches the adjuster without having passed through all three agents | Enforced by orchestration state machine; verified by run-trace inspection (any run ID) |

## 5. Requirements (BR-xx) — grows per epic

Numbering matches the epics in the engineering plan; acceptance criteria are filled in as each is implemented, and the traceability matrix (§8) tracks status.

| ID | Requirement | Acceptance criteria (summary) |
|---|---|---|
| BR-01 | Public repo with governance (branch protection, PR/issue templates, CODEOWNERS, CI) exists before feature work begins | Repo created, protected `main`, templates present |
| BR-02 | Clean Architecture solution skeleton with enforced inward dependency direction | `Domain`/`Application` reference no LLM/vector/web-framework SDK; verified by project reference graph |
| BR-03 | Provider abstraction: `ICompletionService`/`IEmbeddingService` ports with OpenAI + Ollama implementations and a documented fallback chain | Swapping provider is config + one adapter (ADR-0003); fallback chain unit-tested with fakes, no network dependency |
| BR-04 | Synthetic corpus of ≥30 documents / 150+ pages, no real personal data, with ≥2 dated policy versions carrying documented substantive differences (D2's named version-risk) and at least one document category requiring OCR (T6) | 109 documents / 156 pages generated reproducibly from `seed-data/generate/`; scanned claim forms verified to have no extractable text layer (`pdftotext` → ~0 bytes) |
| BR-05 | Relational store (MSSQL via EF Core, with migrations) and vector store (Qdrant) both genuinely wired, not just referenced — includes the first real domain entity (`Document`, driven by FR-1's ingestion-tracking requirement) and readiness health checks proving actual connectivity | Migration applied against a live containerized MSSQL; `/health/ready` returns Healthy only when both MSSQL and Qdrant respond; 4 Testcontainers-based integration tests exercise the repository against a real (ephemeral) SQL Server, including a SQL-Server-enforced unique-constraint case |
| *(BR-06+)* | Ingestion, retrieval, evaluation, multi-agent workflow, realtime/UI, auth, remaining observability (correlation ID, tracing, cost accounting — health/readiness is the only piece BR-05 covers), security, T6 document out | To be added as each is built — see epics in the engineering plan |

## 6. Out of scope (explicit)

- Real personal data of any kind (hard non-negotiable per assessment brief) — corpus is synthetic/public only.
- A third or later insurance product line beyond a single representative policy family used to build the corpus.
- Anything beyond T6's core (OCR confidence handling + one generated document type) — broader document-type support is deferred, see `docs/SYSTEM-DESIGN.md` gap table.
- Semantic/vector search over case data (declarations, claims) — deliberately out of scope; see ADR-0004. Case data is fetched by exact key (policy/claim number), never searched, and is not embedded into Qdrant.

## 7. Assumptions & risks

- **Assumption**: "invitation email" variant statement (D2T6) is authoritative and does not need National-ID derivation math shown.
- **Assumption**: "the document corpus" that FR-2's "ask with citations" retrieves over means the knowledge corpus (policy wordings, exclusions, endorsement templates, reference material) specifically, not case data (declarations/claims) — see ADR-0004.
- **Risk (named by the brief)**: wrong-policy-version retrieval — mitigated by mandatory version/date metadata filtering (see ADR-0004).
- **Risk (named by the brief)**: arithmetic hallucination — mitigated structurally by `PayoutCalculationService` (see `docs/adr/`).
- **Risk (schedule)**: only 6 calendar days remain versus the brief's 12-day assumption; MoSCoW floors (3 agents+orchestrator, 30-doc corpus, one retrieval enhancement, T6 core only) are treated as the actual target. Any further cut will be logged in the `SYSTEM-DESIGN.md` gap table, not silently dropped.

## 8. Traceability matrix

| BR-xx | Implemented? | Evidence |
|---|---|---|
| BR-01 | Implemented | `.github/`, `LICENSE`, `CONTRIBUTING.md`, `.env.example`, branch protection on `main`, CI green |
| BR-02 | Implemented | `DomainCopilot.sln`, `src/*` project reference graph (Domain has no external deps; Application depends only on Domain) |
| BR-03 | Implemented | `src/DomainCopilot.Application/Providers/`, `src/DomainCopilot.Infrastructure/Providers/`, `docs/adr/0003-provider-abstraction-fallback-chain.md`, `tests/DomainCopilot.Application.Tests/Providers/FallbackCompletionServiceTests.cs`, `tests/DomainCopilot.Contract.Tests/KernelToolMapperTests.cs` |
| BR-04 | Implemented | `seed-data/corpus/` (109 docs / 156 pages), `seed-data/generate/facts.py`, `seed-data/README.md` |
| BR-05 | Implemented | `src/DomainCopilot.Domain/Documents/`, `src/DomainCopilot.Infrastructure/Persistence/`, `src/DomainCopilot.Infrastructure/VectorStore/`, `tests/DomainCopilot.Integration.Tests/DocumentRepositoryTests.cs`, `tests/DomainCopilot.Domain.Tests/Documents/DocumentTests.cs` |
