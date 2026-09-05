# ADR-0010: T6's OCR pipeline — real per-page confidence, not a pass/fail flag

**Status**: Accepted
**Date**: 2026-09-05

## Context

T6 (this project's assigned twist) requires OCR of scanned claim documents with confidence handling on the way in — the brief's own framing is explicit that a low-confidence page must not be silently trusted as ground truth alongside a genuinely clean extraction. This is a real, not hypothetical, distinction for this corpus: `seed-data/generate/scanned_pdf.py` produces image-only PDFs (no text layer) of claim intake forms — a category ADR-0004 already scoped as case data, never routed through the knowledge-corpus ingestion pipeline FR-1 governs, since these are per-claim paperwork, not searchable policy text.

The two mechanisms available — extracting text from a scanned image, and knowing how much to trust that extraction — are genuinely different problems needing verification separately: an OCR engine can confidently misread a page, or can also genuinely fail to find anything and (as verification below found) still report a misleadingly high number for that failure.

## Decision

Two external processes, shelled out to rather than wrapped by a native binding: poppler's `pdftoppm` rasterizes each PDF page to a PNG (`IPdfRasterizer`), and Tesseract's own CLI OCRs each image, requested in TSV output mode specifically so the recognized text and Tesseract's real per-word confidence come from one invocation (`IOcrEngine`). Both are ports in `DomainCopilot.Application.Ocr`; `DomainCopilot.Infrastructure.Ocr` provides the only implementations, matching the rest of this codebase's SDK-isolation discipline.

`ScannedDocument.RecordOcrResult` (Domain) computes each page's mean word confidence and the document's overall/lowest-page confidence, then decides `Completed` vs. `NeedsReview` from a single constant (`ConfidenceThresholdPercent = 80.0`) — one page below the threshold routes the *whole* document to review, not just that page, so a bad page can't hide among otherwise-clean ones. This mirrors `IngestionStatus`'s "don't fail silently" principle (FR-1) extended to OCR's own failure mode: a bad scan, not a bad file.

`OcrIngestionService` is idempotent on content hash per claim number (same principle as `KnowledgeIngestionService`, ADR-0004/FR-1) — re-uploading an unchanged scan returns the existing record rather than re-running two external processes for nothing.

## A real bug found by actually running this against a bad scan, not assumed away

Before writing any of this, the actual OCR mechanism was verified by hand against a real corpus file (`seed-data/corpus/claims/intake_clm_2025_04417.pdf`) — confirming what Tesseract's TSV output genuinely looks like for both a clean page and a real confidence number, rather than trusting its documentation. That same live-testing discipline caught a real defect after the code was written: uploading a heavily degraded version of that same scan (blurred, noised, low-resolution) through the actual running API returned `status: Completed` with `overallConfidencePercent: 95` — and an empty `combinedText`. Tesseract had emitted a single level-5 TSV row spanning the *entire page*, with empty recognized text and a confidence value that looked like a real detection. Counting that toward the average silently marked a page that yielded zero actual text as high-confidence — the exact failure mode this whole feature exists to prevent, reproduced by the feature's own first real bug.

Fixed by excluding any word-level row with empty/whitespace text from the confidence average (`TesseractOcrEngine.ParseTsv`) — a row with no recognized content carries no meaningful confidence signal regardless of what number Tesseract attaches to it. Re-verified against the same degraded file afterward: `NeedsReview`, `0%` confidence, empty text — now honestly reflecting what actually happened. The clean scan was re-verified too, to confirm the fix didn't change the good-case result (still `Completed`, ~95%).

## Alternatives considered

- **A managed OCR library / native Tesseract binding (e.g. the `Tesseract` NuGet package)** — rejected for this round. A native binding needs `libtesseract`/`liblept` resolvable through .NET's P/Invoke marshaling, which is exactly as environment-sensitive as the CLI approach (see Consequences) but with a worse failure mode: a marshaling/native-library mismatch throws deep inside the binding rather than surfacing as a plain, greppable non-zero exit code and stderr text. Shelling out to the CLI is a deliberately boring, easy-to-debug integration point for a first working version.
- **A fixed pass/fail flag instead of a numeric threshold** — rejected because the brief specifically asks for confidence *scoring*, not a binary signal, and a numeric value lets a future reviewer see how close a borderline page actually was rather than only that it failed some undisclosed check.
- **Per-page review status instead of routing the whole document to review** — considered, since it's more granular. Rejected for this round: `ScannedDocument` has no per-page status field, only per-page results within one document-level status, and a partially-trusted document (some pages ground truth, others not) is a more complex UX/data-model problem than this round's time budget covers — the whole-document routing is the honest, simpler choice, not a hidden simplification.

## Consequences

Easier: the confidence signal an adjuster sees is a genuinely computed number from a real OCR pass, not a placeholder — `docs/EVALUATION.md`'s eventual adversarial cases (indirect prompt injection via an OCR'd document) have a real pipeline to inject through, not a stub.

Harder: this is the first place this codebase shells out to an external process rather than calling an HTTP API (Ollama/OpenRouter) or an in-process SDK. `OcrOptions` makes the binary paths, library path, and tessdata prefix all independently configurable specifically because of this: the dev machine this was built on has no root access and had to extract Tesseract's `.deb` packages into a user-local prefix (`~/.local/opt/tesseract`, with `LD_LIBRARY_PATH`/`TESSDATA_PREFIX` set via `.env`'s `Ocr__*` keys) rather than a normal system install — CI and any machine with a normal `apt install tesseract-ocr poppler-utils` need none of those overrides, which is exactly why they're options with sane defaults rather than hardcoded paths. A real, disclosed limitation for later: `ScannedDocument` has no reviewer-correction workflow (a human editing a `NeedsReview` document's text and re-approving it) — it surfaces the state honestly but doesn't yet let anyone act on it beyond that signal.

## Verification

Real, not stubbed, at every layer:
- **Domain** (`ScannedDocumentTests`, 8 tests): the `Completed`/`NeedsReview` threshold decision, including the exact-threshold boundary, and `MarkFailed`'s guard clauses.
- **Application** (`OcrIngestionServiceTests`, 5 tests, fakes for the two ports): the completed/needs-review/failed outcomes and idempotency, including that two different claims uploading byte-identical content are *not* treated as the same upload.
- **Integration** (`OcrPipelineTests`, 2 tests, the real Tesseract/`pdftoppm` binaries against a real corpus file): a clean scan OCRs above the review threshold and contains the real claim number and insurer name; a much-lower-DPI rasterization of the same file measurably reduces Tesseract's own reported confidence. Runs correctly in CI (a plain `apt-get install tesseract-ocr poppler-utils` step added to the workflow) and locally via the `Ocr__*` environment-variable overrides.
- **Live, through the real running API**: a real 2-page scanned claim intake form uploaded via `POST /api/ocr/documents` returned accurate extracted text (name, VIN, claim/policy numbers, narrative) at ~95% confidence, `Completed`; re-uploading the identical file returned the same record (idempotency); a genuinely degraded version of the same file correctly returned `NeedsReview` at 0% confidence, empty text — the exact bug described above, confirmed fixed against the real API, not just the unit tests.
