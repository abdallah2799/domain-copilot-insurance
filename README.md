# Domain Copilot — Insurance Claims Adjudication

An agentic RAG platform for insurance claims adjudication: ingests policy/claim documents, answers questions with verifiable citations, and runs a multi-agent adjudication workflow (coverage matching → exclusion analysis → drafting) with a mandatory human approval gate before any decision is finalized.

Built for the ITI Technical Instructor technical assessment.

## Assigned variant

- **Domain: D2 — Insurance claims adjudication**
- **Twist: T6 — Document in/out** (OCR of scanned documents with confidence handling, plus a generated DOCX/PDF adjudication memo with citations and tables)
- Variant as stated in the assignment invitation email (not derived from National ID).

## Starter template declaration

No starter template or boilerplate repository was used. This repository is built from scratch.

## Status

This repository is under active development. The sections below (quick start, environment variables, 5-minute demo path, seeded accounts, troubleshooting) will be filled in as each corresponding part of the system is built — see `docs/SYSTEM-DESIGN.md` and the project board/milestones for current progress.

## Documentation

- [`docs/BRD.md`](docs/BRD.md) — business requirements, personas, traceability matrix
- [`docs/SYSTEM-DESIGN.md`](docs/SYSTEM-DESIGN.md) — target architecture (Part A) and implemented MVP with gap table (Part B)
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — C4 diagrams, sequence/data-flow/ER diagrams, ADRs
- [`docs/SECURITY.md`](docs/SECURITY.md) — OWASP Web/LLM Top 10 controls
- [`docs/EVALUATION.md`](docs/EVALUATION.md) — golden set results and interpretation
- [`docs/AGENTIC-WORKFLOW.md`](docs/AGENTIC-WORKFLOW.md) — how AI tooling was configured and used to build this repo
- [`docs/AI-USAGE-LOG.md`](docs/AI-USAGE-LOG.md) — running log of AI-assisted work, including mistakes caught
- [`teaching/`](teaching/) — slides, lab sheet, and assessment map for a 90-minute session built from this system

## License

[MIT](LICENSE)
