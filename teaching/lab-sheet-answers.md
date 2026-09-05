# Lab Sheet — Answer Key

Instructor-only. Every numeric answer below was verified by actually running `dotnet test`/the calculator, not derived by hand and assumed correct.

## Exercise 1

10/10 tests pass (`dotnet test --filter "FullyQualifiedName~StandardPayoutCalculatorTests"` → `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10`). Per-test edge case:

| Test | Edge case it locks in |
|---|---|
| `Calculate_DamageBelowLimit_SubtractsDeductibleFromDamage` | The ordinary case: damage under the limit, deductible subtracted normally |
| `Calculate_DamageAboveLimit_CapsAtLimitBeforeSubtractingDeductible` | **Order matters**: cap at the limit *first*, then subtract the deductible — not the reverse |
| `Calculate_DamageEqualsLimit_DeductibleStillApplied` | Damage exactly equal to the limit is still capped (a no-op cap), deductible still applies |
| `Calculate_CappedDamageBelowDeductible_FlooredAtZero_NotNegative` | A payout can never go negative — floored at 0 |
| `Calculate_GlassOnlyWaiverApplies_DeductibleIgnoredEvenWhenNonZero` | The glass waiver skips the deductible term entirely, even if it's non-zero |
| `Calculate_ZeroDamage_ReturnsZero` | Zero damage in → zero payout out, no edge-case surprise |
| `Calculate_ZeroDeductible_ReturnsCappedDamageUnchanged` | A zero deductible changes nothing else about the formula |
| `Calculate_NegativeEstimatedDamage_Throws` | Negative inputs are rejected outright, not silently clamped to zero |
| `Calculate_NegativeApplicableLimit_Throws` | Same, for the limit |
| `Calculate_NegativeApplicableDeductible_Throws` | Same, for the deductible |

## Exercise 2

| estimatedDamage | applicableLimit | applicableDeductible | waiver? | actual |
|---|---|---|---|---|
| 8,000 | 25,000 | 1,000 | false | **7,000** — `min(8000,25000) - 1000` |
| 30,000 | 25,000 | 1,000 | false | **24,000** — `min(30000,25000) - 1000 = 25000-1000` |
| 900 | 25,000 | 1,000 | false | **0** — `min(900,25000)-1000 = -100`, floored |
| 900 | 25,000 | 1,000 | true | **900** — deductible term skipped entirely by the waiver |

## Exercise 3

A plain `JsonSerializer.Deserialize<SomeRecord>` into a record with a non-nullable `decimal ApplicableDeductible` property would, on a missing JSON field, silently bind it to `0` (the type's default) — the deserializer has no concept of "this field was actually absent" once binding succeeds. `ToolArguments.RequireDecimal` instead explicitly checks `root.TryGetProperty(...)` and throws `ToolArgumentException` if the field is missing or the wrong JSON kind. The difference matters here specifically because a silently-defaulted deductible of `0` looks like a *completely valid, higher payout* — not an obviously broken result a human would catch on sight. A loud failure (visible in logs, and the agent's turn ends in `ToolExecutionResult.Failed` rather than a wrong number quietly flowing into a recommendation) is the only safe behavior for a missing dollar-figure input.

## Exercise 4

1. `"required": ["estimatedDamage", "applicableLimit", "applicableDeductible"]` — `glassOnlyDeductibleWaiverApplies` is optional (defaults to `false` via `OptionalBool(...) ?? false`). A call missing any of the three required fields hits `RequireDecimal`'s `ToolArgumentException` path and the whole tool call fails cleanly (`ToolExecutionResult.Failed`), which becomes a `ChatMessage.ToolResult` telling the model its call failed — the model sees the failure and can retry with corrected arguments, rather than the pipeline silently continuing on bad data.
2. `AgentRunner`'s tool-calling loop receives a `ToolCall` from the model containing a tool *name* (`"calculate_standard_payout"`) as a plain string — it has no compile-time reference to `StandardPayoutToolExecutor` the class. It resolves the concrete implementation to call by looking up that name against the `IToolExecutor` collection it was given (`toolsByName = availableTools.ToDictionary(t => t.Definition.Name)`), which is exactly why every tool needs the `IToolExecutor` registration in DI — that's the contract `AgentRunner` actually depends on, not any tool's concrete type.

## Stretch Challenge 1

`applicableDeductible == applicableLimit` (e.g. damage 5,000, limit 1,000, deductible 1,000): `min(5000,1000) - 1000 = 1000 - 1000 = 0`. Payout is exactly zero, not negative — same floor-at-zero behavior as `Calculate_CappedDamageBelowDeductible_FlooredAtZero_NotNegative`, just reached via a different combination of inputs.

## Stretch Challenge 2

The trap this challenge is designed to expose: a naive `return payout < 50m ? 50m : payout;` turns a *correct* $0 (fully absorbed by the deductible) into an incorrect $50. The correct fix distinguishes "payout would have been positive but small" from "payout is genuinely zero" — e.g. compute the pre-floor value, and only apply the $50 floor when that pre-floor value is `> 0`:

```csharp
var rawPayout = cappedDamage - deductible;
if (rawPayout <= 0m) return 0m;
return Math.Max(rawPayout, 50m);
```

A trainee's solution is correct if (and only if) it passes both: a genuinely-zero case (damage fully under the deductible) still returns exactly `0`, and a small-but-positive case (e.g. payout would be $20) returns `50`.

## Stretch Challenge 3

Compare against the real `tests/DomainCopilot.Application.Tests/Adjudication/StandardPayoutToolExecutorTests.cs` already in the repo — a trainee's version should exercise at minimum: a valid call returning the correct JSON payout, and a call missing a required field returning `ToolExecutionResult.Failed` rather than throwing an unhandled exception.

## Wrap-up question

The honest answer lives in `docs/EVALUATION.md`'s groundedness section and the harness's own `groundedOk` check (`tools/DomainCopilot.EvaluationHarness/Program.cs`) — it's a **proxy**, not a semantic judge: it only verifies that every citation the model prints corresponds to a document that was actually retrieved, not that the *number* in the model's prose matches the tool's actual output byte-for-byte. This is a real, named gap in the current evaluation harness, not a solved problem — a good trainee answer should recognize that "cites a real document" and "accurately restates that document's/tool's content" are two different claims, and only the first one is currently machine-checked.
