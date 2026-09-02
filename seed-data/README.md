# Seed corpus — Meridian Mutual Personal Auto (synthetic)

`corpus/` is the committed output: **109 documents, 156 pages**, entirely synthetic (no real people, companies, addresses, or records) — see `generate/facts.py` for the master fact sheet everything is derived from. This satisfies the brief's ≥30 document / 150+ page floor with margin.

## What's in the corpus

| Category | Contents | Formats |
|---|---|---|
| `policy-forms/` | Two dated Policy Wording editions (PAP-2024-STD, PAP-2025-STD) with four deliberate, documented differences between them, plus their matching Standard Exclusions Addenda | PDF |
| `declarations/` | One Declarations page per synthetic policyholder (18), stating their specific limits, deductibles, and endorsements | DOCX |
| `endorsements/` | 8 distinct endorsement form templates (ride-share, custom equipment, GAP, etc.) | DOCX |
| `claims/` | Per-claim (18 claims): repair estimate, adjuster case notes (walking the five-step adjudication sequence), a **scanned** claim intake form, and — where applicable — a **scanned** incident report summary | DOCX + image-only PDF |
| `reference/` | Adjudication guidelines, underwriting guidelines, SIU fraud indicators, total-loss methodology, Ohio regulatory reference, glossary, FAQ, training scenarios, version-comparison matrix | PDF |

`manifest.json` lists every document with its category, format, and any associated policy/claim number and form version — useful for building ingestion test fixtures without re-deriving this from filenames.

## Why this shape

- **Two dated policy versions with real, cited differences** (glass deductible waiver, rental limit, ride-share exclusion, claim-acknowledgment timeline) exist specifically to test D2's named risk — retrieval must be version/date-aware, or it will confidently answer with the wrong policy's terms. `version_comparison.md`/`training_scenarios.md` exist to make this testable, not just theoretical.
- **The scanned claim intake forms and incident reports are genuinely image-only PDFs with no text layer** (verified: `pdftotext` on them returns ~0 bytes) — OCR (T6) is required to ingest them, not optional.
- **The adjudication guidelines state the exact payout formula** (`payout = min(estimated_damage, applicable_limit) - applicable_deductible`) that the product's `PayoutCalculationService` (Epic E) implements as deterministic, unit-tested C# — the corpus and the code are meant to agree, not coincidentally match.
- **A handful of claims are deliberately not clean-approve cases**: one predates its policy's effective date (anomaly), one mentions ride-share use ambiguously with no endorsement attached (exclusion-analysis test), one requests a coverage part the policy doesn't have. These are meant to seed the evaluation golden set's adversarial/ambiguous cases (Epic D), not just the happy path.

## Regenerating

```bash
cd seed-data/generate
python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
.venv/bin/python build_corpus.py
```

Requires `soffice` (LibreOffice headless, for DOCX→PDF conversion of the prose documents) on PATH. The script is idempotent — it wipes and rebuilds `seed-data/corpus/` from `facts.py` and `content/*.md` each run, so the committed corpus is always reproducible from source, not hand-edited.

`generate/.venv/` and `generate/out/` are build artifacts and are gitignored; only `generate/*.py`, `generate/content/*.md`, `generate/requirements.txt`, and the resulting `corpus/` are committed.
