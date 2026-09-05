# Teaching Slides — Domain Copilot (90-minute session)

Format: one `##` heading per slide, separated by `---` (Marp/reveal.js/pandoc-compatible). 22 slides — comfortably within the 15–25 floor. Speaker notes are the paragraph under each slide, not meant to be read verbatim on-screen.

---

## 1. Domain Copilot: an Agentic RAG System for Claims Adjudication

A working, tested example of retrieval-augmented generation plus a supervised multi-agent workflow, built for a real domain: insurance claims adjudication (variant D2, twist T6).

*Speaker note: set expectations — this session teaches the architecture and the specific guardrails that make an LLM-based system trustworthy enough to sit in front of a real financial decision, not "how to prompt an LLM."*

---

## 2. Learning outcomes for this session

By the end, a trainee should be able to: (1) explain why "grounded or refused" beats "always answer," (2) name the two failure modes this domain specifically guards against and how, (3) trace one request from HTTP call to LLM call and back, (4) explain why an LLM must never compute money.

*See `teaching/learning-outcomes.md` for the full outcome→assessment mapping.*

---

## 3. The problem, concretely

An adjuster cross-references policy wording, coverage schedules, and exclusion clauses by hand. Two specific failure modes make this risky to automate naively: (1) applying the **wrong policy version** to a claim, (2) trusting an **LLM's arithmetic** for a payout figure.

---

## 4. RAG in one slide: grounding vs. hallucination

Retrieval-Augmented Generation = retrieve real text first, then ask the model to answer *using only that text*, with citations. The alternative — asking the model cold — means it can state something false with total confidence. Grounding doesn't eliminate this risk; it gives you a citation to check.

---

## 5. Architecture: Clean Architecture, four layers

`Domain` (entities, zero dependencies) ← `Application` (use cases, ports/interfaces, zero SDK dependencies) ← `Infrastructure` (every concrete SDK: EF Core, Qdrant, Semantic Kernel) ← `Api` (composition root). Dependencies only ever point inward — mechanically enforced by the project-reference graph, not just a convention.

---

## 6. Why layering like this matters here specifically

If a payout calculation lived in `Infrastructure` next to the LLM adapter, it would be easy to accidentally let the model "help" with it. Putting deterministic math in `Domain` — the layer with *zero* external dependencies, not even an HTTP client — makes it structurally impossible to wire an LLM call into that code path.

---

## 7. Ingestion: extract → clean → chunk → embed → index

Every policy document goes through this pipeline once, idempotently (re-running on an unchanged document is a no-op, checked by content hash). Each chunk keeps its `document title`, `section`, `page`, `form version`, and `effective date` — citation metadata, not just raw text.

---

## 8. Retrieval: hybrid, not just semantic search

Dense (vector/cosine similarity via Qdrant) **and** keyword (BM25) search, fused with Reciprocal Rank Fusion. Semantic search alone can miss an exact term match (a specific clause number); keyword alone misses paraphrase. Fusing both is the "justified enhancement" this project chose over, e.g., a re-ranker.

---

## 9. The named risk: wrong policy version

Two policy editions can define the *same-named* clause differently, in force on different dates. Retrieval **must** filter by the claim's date-of-loss before ranking, not after — a document from the wrong edition should never even be a candidate, not just rank lower.

---

## 10. Refusal: the other half of grounding

If retrieval doesn't find strong enough evidence, the system says so — explicitly, with no completion call spent — rather than letting the model guess from weak matches. This is a real, measured signal (`docs/EVALUATION.md`'s refusal-correctness metric), not just a talking point.

---

## 11. Four agents, one fixed pipeline

Coverage Matcher → Anomaly Analyst → Exclusion Analyst → Adjudication Drafter. A **fixed pipeline/state-machine**, not an open-ended planner that decides its own steps — this is itself a security control (bounds what an agent can even attempt to do).

---

## 12. How an agent "does" anything: the tool-calling loop

An agent is a loop: call the model with a system prompt + a *restricted* list of tools it may call → the model either answers or requests a tool → the tool runs (real code, not the model) → the result goes back to the model → repeat, bounded by a max-iteration breaker.

---

## 13. Today's deep dive: the math guardrail

*(This is the segment the 10-minute teaching video walks through live — chosen because it's small, self-contained, and needs no other slide's context to understand.)*

**The rule**: no agent, ever, computes a payout, limit, or deductible figure itself. It calls a tool. The tool is plain C#, unit-tested, and the agent must cite the tool's own output verbatim.

---

## 14. Why this specific guardrail, and why it's non-negotiable

LLMs are pattern-matchers over token sequences, not calculators — they can produce a plausible-looking wrong number with full confidence, and nothing about the output *looks* wrong. For a domain where the output is literally a dollar amount, "usually right" is not an acceptable bar.

---

## 15. What the code actually looks like

A calculator lives in `Domain` (e.g. `StandardPayoutCalculator`) — a pure function, no I/O, fully unit-tested including edge cases (a claim under the deductible, a claim at exactly the policy limit). A matching `IToolExecutor` (`StandardPayoutToolExecutor`) is the *only* way an agent can reach it, with strict, presence-checked argument validation — a missing amount fails loudly rather than silently defaulting to zero.

---

## 16. Live demo (see the lab sheet, Exercise 2)

Run the Domain-layer unit tests for one calculator, then trace a real tool call end-to-end: an agent's request → `IToolExecutor.ExecuteAsync` → the calculator → a JSON result the agent must quote back, citation and all.

---

## 17. The human approval gate

No recommendation reaches a terminal state without passing through every prior stage **and** an explicit adjuster decision (approve / reject / edit-and-approve). This is enforced by the case's own state machine — an out-of-order transition throws — not a UI convention a determined caller could skip.

---

## 18. This project's twist (T6): documents in and out

**In**: scanned claim forms get OCR'd with *real* per-page confidence (not a fake pass/fail) — a low-confidence page routes the whole document to human review rather than trusting a bad extraction. **Out**: a generated PDF adjudication memo, built from the same cited stage data the chat answer uses.

---

## 19. Prompt injection: the risk agentic systems add

A malicious instruction can arrive three ways: directly in a question, quoted/disguised as fake "policy text," or hidden in an uploaded document's OCR'd text. This system's defense is structural: the user's question (and OCR text) occupies its own prompt slot, never the slot real retrieved chunks occupy — so it can't pass as authoritative content no matter how it's phrased.

---

## 20. Measuring honestly: the evaluation harness

A 32-question golden set (25 normal, 7 adversarial) scored on refusal correctness, retrieval hit-rate, a groundedness proxy, and injection resistance — run against the *real* live API, not simulated. `docs/EVALUATION.md` reports real failures found this way, including one that reproduces the wrong-policy-version risk for real, not just in theory.

---

## 21. Two more guardrails: who can act, and who's watching

Two server-enforced roles (only an Adjuster can finalize a decision; an Analyst can't see another Analyst's cases) — enforced in the API, not just hidden in the UI. Every request carries a correlation ID into logs and a distributed trace, and every LLM call's token usage is persisted, so cost and behavior are inspectable after the fact, not just at the moment it happened.

---

## 22. Lab, mistakes, and where to go from here

Hands-on lab: `teaching/lab-sheet.md`. Common mistakes trainees make with this material: `teaching/common-mistakes.md`. Full architecture reference: `docs/ARCHITECTURE.md`; every design decision's own reasoning: `docs/adr/`.
