# Agentic coding workflow

This project is being built with Claude Code as a governed participant, not a code-generation shortcut. This document tracks what's actually configured and used, updated as it grows — items marked "planned" are not yet real and are listed so scope isn't overstated.

## What's configured

1. **Project instruction file** — [`CLAUDE.md`](../CLAUDE.md), read automatically at the start of every session. Encodes the Clean Architecture dependency-direction rules (which project may reference which), the deterministic-math rule for payout/limit/deductible calculations, the prompts-as-versioned-files rule, and process rules (no direct commits to `main`, Conventional Commits, docs updated in the same PR as the behavior they describe). This is the mechanism that keeps the AI from casually reaching across a layer boundary it would otherwise not "know" about.
2. **Plan-mode gating** — Claude Code's plan mode was used before any file was created: the assignment PDF and stack decisions were turned into a written plan (variant, architecture, MoSCoW backlog, day-by-day pacing) and required explicit approval before any repository or code change happened. Mid-review, four specific corrections were made to the plan (target framework, incremental-not-dumped documentation, the second auth role not being fixed, and the real 6-day timeline) before implementation started — this is the main place "how deliberately the AI is directed" shows up so far.
3. **Structured clarification instead of silent assumptions** — rather than guessing the Domain/Twist variant, the correct GitHub account, the CI runner target, or the LLM provider pairing, the session used a structured question tool to force an explicit choice on each, since guessing wrong on the variant specifically would have invalidated the whole submission per the brief.

## Planned (not yet real — tracked so this list stays honest)

- **Sub-agents scoped to distinct roles** (a security-reviewer pass against `docs/SECURITY.md`'s OWASP checklist, a test-writer pass for the domain/application unit tests) — planned once there's enough implemented surface area to review.
- **Custom commands** for repeated operations (e.g. "run the evaluation harness and update `docs/EVALUATION.md`") — planned once the harness exists (Day 2 onward).
- **Versioned prompt library** for the product's own agents (Coverage Matcher, Exclusion Analyst, Adjudication Drafter prompts under `prompts/`) — planned for Day 3 (multi-agent workflow).
- **Hooks enforcing quality gates automatically** — not yet configured; candidate is a pre-commit-style check that blocks a commit touching `src/DomainCopilot.Domain` or `.Application` if it adds a reference to a disallowed package, mechanically enforcing the rule `CLAUDE.md` states declaratively.

## Where the agentic approach has already failed / needed correction

- The AI's first attempt to patch a flagged vulnerable NuGet package (`Microsoft.OpenApi` 2.0.0 → 2.0.1) was itself insufficient — the advisory's actual patched range starts at 2.7.5 for the 2.x line. Caught by re-checking the GitHub Security Advisory API directly rather than trusting the version-bump alone. This is now written into `CLAUDE.md` as a standing rule.
- Left to its own judgment, the plan initially targeted .NET 8 and would have written all "must-have" documentation as a single late-stage pass on the assumed 12-day window — both were corrected by direct user review before any code was written. Neither would have been caught by re-reading the assignment PDF alone; both needed a domain-expert pass on the plan.

See [`docs/AI-USAGE-LOG.md`](AI-USAGE-LOG.md) for the running, dated log of delegated-vs-hand-verified work.
