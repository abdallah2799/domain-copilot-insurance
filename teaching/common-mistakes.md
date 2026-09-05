# Five Common Trainee Mistakes

Written from what actually went wrong or was easy to misunderstand while *building* this system, not guessed in the abstract.

## 1. Confusing "the model refused" with "the system is structurally injection-resistant"

A trainee will often point to a passing injection test (e.g. AQ-03 in `docs/EVALUATION.md`) and conclude "the model is smart enough to resist injection." That's true for that one run, on that one model, that one time — it's a probabilistic property of the specific model, not a guarantee. The actual structural guarantee is one level down: `AskService` never places user-supplied text in the same prompt slot real retrieved corpus content occupies (see `docs/SECURITY.md`, LLM01), so even a model that *wanted* to comply couldn't make a fake instruction indistinguishable from real policy text. Push trainees to find that code, not just cite a passing test.

## 2. Assuming "hybrid retrieval" means "better search," not "a specific, checkable guardrail"

Trainees new to RAG often treat dense+keyword fusion as a generic quality improvement ("more signals = better"). In this system it's specifically a **version-safety** mechanism: the metadata filter (form version / effective date) runs *before* ranking, excluding the wrong policy edition from candidacy entirely — not just down-ranking it. A trainee who can't explain why a wrong-version chunk being ranked #2 instead of #1 is still a failure hasn't understood the actual risk being guarded against.

## 3. Treating a unit-tested calculator as "the whole guardrail"

`StandardPayoutCalculator` being pure, deterministic, and 100%-tested is necessary but not sufficient — the actual guardrail is the *combination* of that calculator plus `ToolArguments`' strict presence validation plus the fact an agent can only reach it through a tool call it must cite verbatim. A trainee who writes correct unit tests for the calculator but can't explain what stops an agent from computing the number itself in its own prose hasn't understood where the real boundary is.

## 4. Reading "refusal" as a UX feature instead of a measured, sometimes-failing control

`docs/EVALUATION.md` reports a real failure (AQ-02, an ambiguous question that wasn't refused) rather than a clean 100% pass rate. Trainees sometimes assume a shipped feature "just works" because it's in the codebase — walk through this specific documented failure to make the point that refusal is a heuristic (retrieval-score density) with a known, real gap (it doesn't detect *question ambiguity*, only *lack of matching material*), not a solved problem.

## 5. Assuming more agents/steps automatically means more safety

A trainee coming from "agent frameworks" content elsewhere sometimes assumes a more autonomous, more-agentic system (letting an agent decide its own next step, add its own tools, etc.) would be *more* capable and therefore better. This project's own ADR-0009 explicitly rejects an open-ended planner in favor of a fixed pipeline/state-machine specifically because it bounds what an agent can attempt — walk through why "the orchestrator decides what runs next, never the model" is a security property (mitigating OWASP LLM06, Excessive Agency), not just a simplicity choice.
