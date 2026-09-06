# ADR-0014 — Record and replay provider completions

- **Status:** Accepted
- **Date:** 2026-09-06
- **Supersedes / relates to:** ADR-0003 (provider abstraction and fallback chain), ADR-0009 (multi-agent orchestrator)

## Context

A full four-agent adjudication run is expensive in *requests*, not just tokens. Each agent runs a tool-calling loop with its own iteration budget — Coverage Matcher 8, Anomaly Analyst 12, Exclusion Analyst 6, plus the Drafter and any graceful-degrade plain-RAG call — so a single end-to-end run can cost up to roughly 30 provider requests, and a well-behaved one still costs a dozen.

The hosted provider this project defaults to (OpenRouter, chosen in ADR-0003 because OpenAI no longer has a perpetual free tier) allows **50 requests per day** on its free tier without a credit purchase. That is about **two end-to-end runs per day**.

Two runs a day is not enough to:

- develop against the workflow at all — one bad prompt edit burns the day's budget;
- run an end-to-end test in CI, ever;
- rehearse and record a demo video, which needs many takes of the same run.

The local Ollama fallback leg exists and works, but is not a substitute here: ADR-0009 documents honestly that the Anomaly Analyst does not converge on an 8B-class local model, and a full local run takes 10+ minutes per stage. So "just use the local model" trades a request budget for a correctness and latency problem.

## Decision

Add **record** and **replay** modes to the completion chain, selected by configuration (`Providers__CompletionMode` = `Live` | `Record` | `Replay`, default `Live`).

- `RecordingCompletionService` decorates the real chain, passes every call through to it, and appends the genuine provider response to a **cassette** — a committed JSON file.
- `ReplayCompletionService` serves responses from that cassette and makes **no network call at all**.

Both are plain implementations of the existing `ICompletionService` port, composed in `Infrastructure`'s DI exactly as `FallbackCompletionService` already is. No Application or Domain code changes, and no agent, orchestrator, or prompt is aware which mode is active.

Requests are matched by a **SHA-256 fingerprint** over everything that can change the model's answer: every message (including prior assistant tool-call requests and their tool results), the tool definitions offered, temperature, and max tokens.

A request with no recorded match is a **hard error**, not a fall-through to the live provider. Falling through silently would both spend the very budget replay exists to protect and make a nominally deterministic run secretly non-deterministic; the error message names the fingerprint and tells you to re-record.

## Consequences

**Good**

- One real recorded run replays unlimited times, instantly, with no network and no budget.
- End-to-end workflow tests become possible in CI.
- The demo video can be recorded in as many takes as needed, deterministically.
- A cassette is reviewable evidence of what the model actually returned — it can be diffed, and the `requestPreview` field makes it readable by eye. That paid for itself immediately: the first recorded run is what exposed the tool-argument rejection loop behind the Anomaly Analyst failure (see ADR-0009) and the Drafter reporting a payout from a tool it had never called — both of which had been invisible for three earlier rounds of investigation that reasoned from iteration counts instead of from the conversation.
- It exercises the provider-abstraction acceptance test the brief requires: a completely different completion backend, added as configuration plus one adapter, with zero business-logic change.

**Bad / limits**

- A cassette is only valid for the exact inputs and prompts it was recorded against. Editing a prompt invalidates it, by design — the fingerprint changes and replay fails loudly rather than serving a stale answer.
- Recording still costs one real run's worth of budget.
- Replay is not a test of the *provider*; it tests everything downstream of the provider. Live mode remains the only thing that exercises the real API contract, so it stays the default.

**Honesty note.** Cassette contents are real captured provider responses, never hand-written. Any use of replay mode for a recorded demo is disclosed in the README rather than passed off as live inference — replay makes a run *repeatable*, it does not make it *fictional*.

**No cassette ships in this repository yet.** The one recorded while building this predates the fixes in #123/#125, so it encodes a run whose Drafter reported a payout it had not calculated — committing it would ship a recording of known-broken behaviour as if it were reference output. `seed-data/cassettes/` is created on the first `Record` run. A cassette recorded against current `main` can be committed once it exists.
