# ADR-0011: The generated adjudication memo — QuestPDF, from the same typed stage data the agents produced

**Status**: Accepted
**Date**: 2026-09-05

## Context

T6's other required half (alongside OCR, ADR-0010) is document-out: a professionally formatted generated work product — the adjudication memo an adjuster reviews — with embedded citations and a coverage/limits table, produced from the same grounded data as the rest of the system, not a separately-written summary that could drift from what the agents actually found. The brief accepts either DOCX or PDF; the cut-order guidance ("keep T6's core: OCR confidence + one generated document type") explicitly permits picking one rather than both.

`AdjudicationCase` (ADR-0008) already persists every stage's typed result as a JSON blob, and the four typed records (`CoverageMatchResult`, `AnomalyFindings`, `ExclusionAnalysisResult`, `Recommendation`) already exist as the agents' own output contracts (ADR-0009). The memo's job is rendering those, not re-deriving anything — including any payout figure, which per ADR-0006 only ever came from a deterministic calculator in the first place.

## Decision

PDF, via QuestPDF (Community license — free for this project's use: an individual, non-commercial, open-source assessment submission, well under the license's $1M revenue threshold). `AdjudicationMemoGenerator` (Infrastructure) is the only class referencing QuestPDF directly, behind `IAdjudicationMemoGenerator` (Application) — the same SDK-isolation discipline every other external dependency in this codebase already follows. `AdjudicationMemoService` (Application) loads a case, deserializes whichever stage JSON blobs it actually has, and hands the generator a fully-typed `AdjudicationMemoData` — the same camelCase `JsonSerializerOptions` `AdjudicationOrchestrator` used to write those blobs, since deserializing them back needs the matching convention.

A memo can be requested (`GET /api/adjudication/runs/{id}/memo`) at any point in a run, not only once a recommendation exists: each of the four stage sections renders "Not yet completed" for a stage that hasn't run yet, and a fifth section surfaces the case's failure reason or the human adjuster's decision when either exists. This is a deliberate design choice, not an oversight: per ADR-0009, a real run does not reliably reach a final recommendation (Anomaly Analyst's documented non-convergence), so a memo endpoint that only worked for fully-decided cases would be untestable against this project's own real data.

## Alternatives considered

- **DOCX via `DocumentFormat.OpenXml`** — rejected for this round per the brief's own "pick one" permission. QuestPDF's fluent, typed layout API (tables, columns, conditional sections) maps onto this memo's actual structure more directly than OpenXml's lower-level part/element model, for less code.
- **A native Tesseract-style CLI tool for PDF generation (e.g., piping through `wkhtmltopdf` or LibreOffice headless, both used elsewhere in this project's own corpus generator)** — rejected: this would trade a well-typed, unit-testable C# API for another external-process dependency (exactly the tradeoff ADR-0010 already accepted once for OCR, where there was no in-process alternative). QuestPDF is pure .NET with no native binary to install, so there's no reason to pay that cost twice.
- **Refusing to generate a memo until the case reaches a terminal/decided state** — rejected; see Decision above. An adjuster benefits from a partial memo showing what's actually known so far, and this project's own real verification data would have nothing to test the endpoint against otherwise.

## Consequences

Easier: the memo can never state a fact the pipeline didn't actually produce — there is no code path in `AdjudicationMemoGenerator` that computes or infers a value, only formats one already sitting in a typed record. Adding a fifth agent stage later (per ADR-0009's own note on the SIU/fraud-specialist gap) means one more section function, not a redesign.

Harder: QuestPDF's Community license is free for this project's actual use but is a real licensing constraint to be aware of if this codebase were ever repurposed commercially at scale — worth restating here rather than only in a NuGet package listing nobody reads twice. There's no memo-editing or re-generation-after-edit workflow yet: an `EditedAndApprove`d case's memo reflects whatever `RecommendationJson` was last recorded, which is the edited version per `AdjudicationCase.EditAndApprove`, but this hasn't been separately verified live since no real run has reached that state during this project's testing (the same Anomaly Analyst gap noted above).

## Verification

Real content verification, not just "generation didn't throw": 4 new tests (`AdjudicationMemoGeneratorTests`) extract the actual text back out of each generated PDF via PdfPig (already a real dependency, used elsewhere for knowledge-corpus extraction — a QuestPDF-produced PDF has a genuine text layer, so this is a direct check, not an OCR approximation) and assert on real facts: a bare case renders all four stages as "Not yet completed"; a fully-progressed, agent-approved case renders the real coverage/anomaly/exclusion/recommendation content, the real payout figure and citations, and the approving adjuster's name, with zero "Not yet completed" text remaining; a degraded/failed case renders its actual failure reason text.

Live, through the real running API: `GET /api/adjudication/runs/{id}/memo` against a real persisted case (real `CoverageMatchResult` from an actual hosted-model run, a degraded fallback failure reason from the documented Anomaly Analyst gap) returned a valid one-page PDF; rasterizing that PDF and running it back through the OCR pipeline (ADR-0010) confirmed the rendered page visually contains the exact claim number, policy number, coverage part, deductible, endorsement, citations, and the full degraded-fallback failure text — the same real data the API already returns as JSON, now confirmed to actually appear correctly laid out on the page, not just present somewhere in the PDF's internal structure.
