# Project rules for AI-assisted work on Domain Copilot

This file is read by Claude Code at the start of every session in this repo. It exists so architectural boundaries are enforced by instruction, not rediscovered each time. See `docs/AGENTIC-WORKFLOW.md` for why this file exists as a deliverable, not just tooling.

## Architecture boundaries (non-negotiable)

- `src/DomainCopilot.Domain` must never reference an LLM SDK, vector-store SDK, web framework, or `DomainCopilot.Infrastructure`/`DomainCopilot.Api`. Zero external package dependencies beyond the BCL.
- `src/DomainCopilot.Application` must never reference Semantic Kernel, `Qdrant.Client`, EF Core provider packages, or ASP.NET Core. It may only reference `DomainCopilot.Domain` and define port interfaces (`ICompletionService`, `IEmbeddingService`, `IVectorStore`, etc.) that `Infrastructure` implements.
- Only `src/DomainCopilot.Infrastructure` may reference concrete provider SDKs. Only `src/DomainCopilot.Api` may reference `Infrastructure` (composition root).
- Any payout, limit, or deductible calculation is plain C# in `Application`/`Domain`, unit-tested, and never delegated to an LLM. Agents may call it as a tool and must cite its output verbatim.
- Prompts live under `prompts/` as versioned files, never as string literals in C#.

## Process rules

- No direct commits to `main`. Every change lands via a PR that references an Issue (`Closes #N`).
- Conventional Commits (`type(scope): summary`); message body explains *why* when not obvious.
- Every doc under `docs/` (`BRD.md`, `SYSTEM-DESIGN.md`, `ARCHITECTURE.md`, `SECURITY.md`, `EVALUATION.md`, ADRs) is a living document — update the relevant one in the same PR that changes the behavior it describes, not in a later "docs" pass.
- Don't write a test that asserts nothing meaningful (e.g. an empty `[Fact]` body) just to pad coverage — delete template placeholder tests instead of keeping them.
- Before claiming a dependency is safe, check it against the current advisory database, not just "it's a newer version number" — see `docs/adr/0001-clean-architecture-layering.md` history for why (an initial `Microsoft.OpenApi` bump from 2.0.0 to 2.0.1 was still vulnerable; only 2.7.5+ was actually patched).

## Style

- No comments explaining *what* code does; only *why*, and only when genuinely non-obvious.
- No speculative abstractions or config knobs for requirements not yet in `docs/BRD.md`.
