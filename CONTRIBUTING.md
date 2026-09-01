# Contributing

This is an individual technical assessment submission (ITI Instructor Task, variant D2T6). It is not open to external contributions during the assessment window, but the workflow below documents the discipline used to build it — this is itself part of what is being assessed.

## Workflow

- No direct pushes to `main`. All changes land via Pull Request, even solo.
- Branch naming: `feat/<short-desc>`, `fix/<short-desc>`, `docs/<short-desc>`, `chore/<short-desc>`.
- Commits follow [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): summary`, explaining *why* in the body when the change isn't self-evident.
- Every PR is self-reviewed with inline comments before merge, links its originating Issue (`Closes #N`), and describes what changed, why, and how it was tested.
- CI (build, lint, tests, dependency scan, secret scan) must be green before merge; branch protection enforces this.

## Local setup

See the Quick Start section of [`README.md`](README.md) once it is populated, and `.env.example` for required environment variables.
