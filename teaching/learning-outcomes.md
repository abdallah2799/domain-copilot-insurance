# Learning Outcomes → Assessment Map

| # | Learning outcome | Taught in (slides) | Assessed by |
|---|---|---|---|
| LO1 | Explain why "grounded or explicitly refused" is safer than "always produce an answer" for a domain with financial/legal consequences | Slides 4, 10 | Lab wrap-up discussion; a trainee should be able to name the refusal mechanism's trigger (`HasSufficientEvidence`) without looking it up |
| LO2 | Name the two failure modes D2 (insurance claims adjudication) specifically guards against, and which mechanism guards each | Slides 3, 9, 13–15 | Lab Exercises 1–4 directly exercise the arithmetic guardrail; a trainee should be able to state the version-risk guardrail from memory (dense+keyword retrieval filtered by date-of-loss before ranking) |
| LO3 | Trace one HTTP request through Clean Architecture's four layers and explain why the dependency direction is enforced, not just conventional | Slides 5–6 | Lab Exercise 4, part 2 (why `IToolExecutor`, not the concrete type, is what `AgentRunner` depends on) |
| LO4 | Explain, with a concrete example, why an LLM must never compute a payout/limit/deductible figure itself | Slides 13–16 | Lab Exercises 1–3 (run real tests, trace the formula by hand, explain the argument-validation guardrail) |
| LO5 | Describe the tool-calling loop mechanically: what the model sees, what it can request, and what actually executes the request | Slides 12, 16 | Lab Exercise 4; Stretch Challenge 3 (write a test that exercises exactly this loop's one step) |
| LO6 | Identify at least one structural (not just probabilistic) defense against prompt injection in this system | Slide 19 | Discussion question: "the user's question text and OCR'd document text share one prompt slot — why does that specific design choice matter for injection resistance?" |
| LO7 | Distinguish a proxy metric from a ground-truth judge, and explain why the evaluation harness's groundedness check is the former | Slide 20 | Lab wrap-up question (answer key: `teaching/lab-sheet-answers.md`) |
| LO8 | Extend an existing deterministic guardrail with a new business rule without breaking its existing invariants | Slide 15–16 | Stretch Challenge 2 — the graded criterion is explicit: does the trainee's solution distinguish "genuinely zero" from "small but positive," or does it silently break the zero-floor case |

## Assessment rubric (for the lab, not a formal exam)

- **Meets expectations**: completes Exercises 1–4 correctly, including the *why* answers (Exercises 3–4 ask for reasoning, not just code that runs).
- **Exceeds expectations**: completes at least one stretch challenge with tests that actually distinguish the edge case the challenge is designed to expose (see the answer key's note on Stretch Challenge 2's trap).
- **Needs follow-up**: can run the existing tests but cannot explain, in their own words, why `RequireDecimal` throwing instead of defaulting matters for this specific domain — this is the single most important concept in the whole lab, and a trainee who can run code but not explain this needs a follow-up conversation, not just a passing grade.
