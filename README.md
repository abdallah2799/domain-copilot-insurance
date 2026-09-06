# Domain Copilot — Insurance Claims Adjudication

An agentic RAG platform for insurance claims adjudication: ingests policy/claim documents, answers questions with verifiable citations, and runs a four-agent adjudication workflow (coverage matching → anomaly detection → exclusion analysis → drafting) with a mandatory human approval gate before any decision is finalized.

Built for the ITI Technical Instructor technical assessment.

## Assigned variant

- **Domain: D2 — Insurance claims adjudication**
- **Twist: T6 — Document in/out** (OCR of scanned documents with confidence handling, plus a generated PDF adjudication memo with citations and tables)
- Variant as stated in the assignment invitation email (not derived from National ID).

## Starter template declaration

No starter template or boilerplate repository was used. This repository is built from scratch.

## Quick start

Prerequisites: .NET 10 SDK, Node 20+, Docker, and (for local LLM fallback) [Ollama](https://ollama.com) with `llama3.1` and `nomic-embed-text` pulled (`ollama pull llama3.1 && ollama pull nomic-embed-text`).

```bash
git clone <this-repo> && cd domain-copilot-insurance
cp .env.example .env   # fill in the values below
```

Fill in `.env` (see [Environment variables](#environment-variables)), then:

```bash
# 1. Bring up the stateful dependencies (MSSQL, Qdrant, the OTel viewer)
docker compose up -d mssql qdrant otel-collector

# 2. Apply EF Core migrations
dotnet ef database update --project src/DomainCopilot.Infrastructure --startup-project src/DomainCopilot.Api

# 3. Run the API (seeds the two demo accounts on first startup)
dotnet run --project src/DomainCopilot.Api

# 4. In a second terminal, run the Angular dev server
cd ui/angular-app && npm install && npm start
```

API: `http://localhost:5080` (or whatever port `dotnet run` reports). Angular: `http://localhost:4200`. OTel dashboard: `http://localhost:18888`.

If you want the local Ollama fallback leg to actually work rather than only the hosted OpenRouter leg, also run `docker compose --profile local-llm up -d ollama` — or, as this dev environment does, run Ollama natively on the host and point `Providers__Ollama__BaseUrl` at `http://localhost:11434` instead of the compose service name (see the comments in `.env.example`).

## Environment variables

See `.env.example` for the full, commented list (provider keys, connection strings, JWT signing key, seeded-account credentials, OCR paths, OTel endpoint). Nothing in `.env` is committed; `.env.example` documents every name with no real values.

Two variables you must set before first run: `SEED_ADJUSTER_PASSWORD` and `SEED_ANALYST_PASSWORD` — `DemoUserSeeder` skips seeding entirely (no accounts created at all) if either is left blank, rather than creating an account with a known-empty password.

## 5-minute demo path

1. **Log in** at `http://localhost:4200/login` with the Analyst account (`SEED_ANALYST_USERNAME`/`SEED_ANALYST_PASSWORD` from your `.env`).
2. **Ingest the corpus**: go to *Ingest*, click the button — this calls `POST /api/ingestion/knowledge-corpus` against `seed-data/corpus/` (60 documents / 151 pages of synthetic policy wording, endorsements, and reference material) and `POST /api/casedata/load` for the structured Declarations/claim-history facts the agents' lookup tools need.
3. **Ask a grounded question**: go to *Ask*, try *"Does the glass-only deductible waiver apply to a collision claim?"* — watch the streamed answer arrive with real citations. Try an out-of-corpus question (*"What is the capital of France?"*) to see the refusal path — no LLM call is made for it.
4. **Try OCR**: go to *OCR*, upload any scanned/image PDF with a claim number — watch it come back with a real per-page confidence score and a `Completed`/`NeedsReview` status.
5. **Run the adjudication workflow**: go to *Adjudication runs → New run*, fill in a claim (any policy/claim number from `seed-data/generate/content/`, or use `CLM-2025-04417` / `MMIC-PAP-100234` which the test suite also exercises), and watch the four agents run live over SSE. This takes anywhere from under a minute (hosted OpenRouter) to 10+ minutes (local Ollama fallback, if OpenRouter's free tier is rate-limited) — see `docs/EVALUATION.md`'s honest scope notes on real observed latency.
6. **Approve, and download the memo**: once the run reaches *Awaiting Approval*, log out and back in as the **Adjuster** account (only this role can act here — try it as the Analyst first to see the real 403), approve the recommendation, then download the generated PDF memo from the run detail page.
7. **Check observability**: go to *Observability* to see real per-call token/cost accounting for everything you just did, and open `http://localhost:18888` for the full distributed trace.

## Seeded accounts

Two accounts are seeded on first API startup (`DemoUserSeeder`) — no self-service registration exists, deliberately (see `docs/adr/0012-authentication-and-object-level-authorization.md`):

| Role | Username | Password |
|---|---|---|
| Analyst | `SEED_ANALYST_USERNAME` (default `analyst`) | `SEED_ANALYST_PASSWORD` — set in your `.env` |
| Adjuster | `SEED_ADJUSTER_USERNAME` (default `adjuster`) | `SEED_ADJUSTER_PASSWORD` — set in your `.env` |

## Running the workflow without a provider budget (record / replay)

A full four-agent run costs up to ~30 provider requests. OpenRouter's free tier allows **50 per day**, so a live provider affords roughly **two end-to-end runs a day** — not enough to develop against, test in CI, or rehearse a demo.

`Providers__CompletionMode` (ADR-0014) solves that by recording one real run and replaying it as often as you like:

```bash
# 1. Record once, against the live provider (costs one run's worth of budget).
#    Every genuine provider response is saved to seed-data/cassettes/adjudication-demo.json.
Providers__CompletionMode=Record dotnet run --project src/DomainCopilot.Api

# 2. From then on, replay it — instant, offline, deterministic, and free.
Providers__CompletionMode=Replay dotnet run --project src/DomainCopilot.Api
```

In `Replay` mode no network call is made at all. A request the cassette doesn't contain fails loudly with the fingerprint that missed, rather than silently falling through to the live provider — so a replayed run is genuinely hermetic. Editing a prompt or changing the claim inputs invalidates the cassette by design; re-record it.

Cassettes hold **real captured provider responses**, never hand-written ones. Replay makes a run repeatable, not fictional. No cassette ships in this repository — record your own with step 1 above, which also creates `seed-data/cassettes/`.

## Troubleshooting

- **Every request returns 401.** Every endpoint except `POST /api/auth/login` requires a bearer token (FR-8). Log in first; the Angular app attaches the token automatically once you're logged in there.
- **A completion call takes 10+ minutes, or times out.** If `Providers__OpenRouter__ApiKey` is unset or rate-limited (free tier: 20 req/min, 50 req/day), every completion falls back to local Ollama, which is genuinely slow for this project's system prompts on modest hardware — this is a real, disclosed constraint (see `docs/EVALUATION.md`), not a bug. Check the API log for `Primary completion provider OpenRouter failed, falling back to Ollama` to confirm which leg is actually running.
- **OCR endpoints fail with "No such file or directory."** `tesseract`/`pdftoppm` aren't on `PATH`. Install `tesseract-ocr poppler-utils` (`apt install tesseract-ocr poppler-utils` on Debian/Ubuntu), or set `Ocr__TesseractBinaryPath`/`Ocr__PdftoppmBinaryPath` in `.env` to their real locations.
- **Connecting to MSSQL/Qdrant fails when running the API with `dotnet run` directly on the host.** The default `.env.example` values use the docker-compose network hostnames (`mssql`, `qdrant`, `ollama`, `otel-collector`), which only resolve from *inside* the compose network. Running the API directly on the host (as this Quick Start does) needs `localhost` and the host-mapped ports instead — see the inline comments in `.env.example` for the exact values.
- **The OTel dashboard shows nothing.** Confirm `docker compose up -d otel-collector` is running and `OTEL_EXPORTER_OTLP_ENDPOINT` points at `http://localhost:4317` (host-run API) — the container's real OTLP/gRPC port is `18889` internally, mapped to the conventional `4317` on the host side (see ADR-0013).

## Documentation

- [`docs/BRD.md`](docs/BRD.md) — business requirements, personas, traceability matrix
- [`docs/SYSTEM-DESIGN.md`](docs/SYSTEM-DESIGN.md) — target architecture (Part A) and implemented MVP with gap table (Part B)
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — C4 diagrams, sequence/data-flow/ER diagrams, ADR index
- [`docs/SECURITY.md`](docs/SECURITY.md) — OWASP Web/LLM Top 10 controls, demonstrated prompt-injection resistance
- [`docs/EVALUATION.md`](docs/EVALUATION.md) — golden-set results (32/32 entries run live) and honest interpretation
- [`docs/AGENTIC-WORKFLOW.md`](docs/AGENTIC-WORKFLOW.md) — how AI tooling was configured and used to build this repo
- [`docs/AI-USAGE-LOG.md`](docs/AI-USAGE-LOG.md) — running log of AI-assisted work, including mistakes caught
- [`docs/adr/`](docs/adr/) — architecture decision records
- [`teaching/`](teaching/) — slides, hands-on lab, learning-outcomes map, and common-mistakes note for a 90-minute session built from this system

## License

[MIT](LICENSE)
