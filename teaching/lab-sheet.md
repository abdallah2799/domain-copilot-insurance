# Hands-On Lab — The Deterministic Payout Guardrail

**Time**: ~25 minutes of the 90-minute session. **Prerequisite**: a clone of the repo, .NET 10 SDK installed. No LLM, database, or Docker container needed for the core exercises — this lab deliberately stays inside `DomainCopilot.Domain`, the zero-dependency layer, so it runs anywhere instantly.

## Setup

```bash
cd domain-copilot-insurance   # repo root
dotnet build src/DomainCopilot.Domain
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`.

---

## Exercise 1 — Run the existing guardrail tests

```bash
dotnet test --filter "FullyQualifiedName~StandardPayoutCalculatorTests"
```

**Expected output**: `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10` (plus a few "No test matches" lines from other test projects — that's normal, the filter only matches one project).

Open `src/DomainCopilot.Domain/Adjudication/StandardPayoutCalculator.cs` and `tests/DomainCopilot.Domain.Tests/Adjudication/StandardPayoutCalculatorTests.cs` side by side. For each of the 10 tests, write down in one sentence *which specific edge case* it locks in (e.g., "damage above the limit — the limit caps first, then the deductible subtracts").

---

## Exercise 2 — Trace the formula by hand

The formula: `payout = max(0, min(estimatedDamage, applicableLimit) - applicableDeductible)`, or `0` if `glassOnlyDeductibleWaiverApplies` is true (then the deductible term is skipped entirely).

Without running any code, compute the expected payout for each row, then run the numbers through a quick C# script (or a REPL — `dotnet fsi`/a scratch test) to check yourself:

| estimatedDamage | applicableLimit | applicableDeductible | waiver? | your answer | actual (`Calculate(...)`) |
|---|---|---|---|---|---|
| 8,000 | 25,000 | 1,000 | false | ___ | 7,000 |
| 30,000 | 25,000 | 1,000 | false | ___ | 24,000 |
| 900 | 25,000 | 1,000 | false | ___ | 0 |
| 900 | 25,000 | 1,000 | true | ___ | 900 |

(Answer key: `teaching/lab-sheet-answers.md`.)

---

## Exercise 3 — Why does argument order matter?

`StandardPayoutToolExecutor.ExecuteAsync` (in `src/DomainCopilot.Application/Adjudication/`) reads its arguments with `ToolArguments.RequireDecimal(root, "estimatedDamage")` rather than a plain `JsonSerializer.Deserialize` into a record.

1. Open `src/DomainCopilot.Application/Providers/ToolArguments.cs` and read `RequireDecimal`.
2. Answer: if an LLM's tool call omitted `"applicableDeductible"` entirely (a malformed call, which real local models do produce sometimes — see `AgentRunner`'s own doc comments), what would a plain `JsonSerializer.Deserialize<SomeRecord>` silently do to a non-nullable `decimal` field? What does `RequireDecimal` do instead, and why does that difference matter *specifically* for a dollar figure?

---

## Exercise 4 — Find the tool's contract

Open `StandardPayoutToolExecutor.Definition` (the `ToolDefinition` with the JSON Schema). This is the *only* description the LLM ever sees of this capability.

1. Which fields are `"required"`? What happens (read `RequireDecimal`/`RequireString`, Exercise 3) if the model's call is missing one?
2. Find where this tool is registered in `src/DomainCopilot.Infrastructure/DependencyInjection.cs`. It's registered as both its concrete type and `IToolExecutor` — write one sentence on why an orchestrator needs the `IToolExecutor` registration specifically (hint: how does `AgentRunner` decide which tool to call when the model asks for `"calculate_standard_payout"` by name?).

---

## Stretch Challenge 1 — Add a new edge case

`Calculate_DamageEqualsLimit_DeductibleStillApplied` exists, but there's no test for **`applicableDeductible` equal to `applicableLimit`** exactly (a very small claim against a very small limit). Write that test, predict its expected value first, then run it and confirm.

## Stretch Challenge 2 — Extend the guardrail

Imagine a new business rule: a **minimum payout of $50** applies to any approved (non-zero) claim, to cover processing cost. Modify `StandardPayoutCalculator.Calculate` to implement this (careful: it must not turn a *correctly-zero* payout — fully absorbed by the deductible — into $50). Write at least 2 new tests proving both the new floor and that a genuine zero-payout case still returns exactly `0`.

## Stretch Challenge 3 — Trace a real tool call end-to-end

Without running the full LLM pipeline: write a small xUnit test in `tests/DomainCopilot.Application.Tests/Adjudication/` that constructs a `StandardPayoutToolExecutor`, builds a JSON arguments string by hand (mimicking what an LLM's tool call would send), calls `ExecuteAsync`, and asserts on the resulting `ToolExecutionResult.ResultJson`. This is exactly the shape of the *real* `StandardPayoutToolExecutorTests.cs` already in the repo — write yours first, then compare.

---

## Wrap-up question (discussion, no code)

`AdjudicationDrafterAgent`'s prompt instructs the model to call a payout tool and then cite its output verbatim. Nothing stops a sufficiently confused model from writing a *different* number in its final prose answer than the tool actually returned. Where in this codebase would you look to catch that gap if it happened in production? (There's a real, honest answer in `docs/EVALUATION.md`'s groundedness-proxy discussion — this question is meant to make a trainee reason about it first, not just look it up.)
