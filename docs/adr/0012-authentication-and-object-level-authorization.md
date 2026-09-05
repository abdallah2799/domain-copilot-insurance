# ADR-0012: Authentication, two server-enforced roles, and object-level authorization

**Status**: Accepted
**Date**: 2026-09-05

## Context

FR-8 requires authentication plus at least two roles with genuinely different, server-enforced permissions, "never enforced only by hiding UI" — and D2's approval gate (ADR-0008) already mandates one of those roles by name: only an **Adjuster** may approve, reject, or edit-and-approve an adjudication run. Before this change, every controller was unauthenticated, and the "who is acting" field on an approval decision (`AdjudicationCase.ApprovedBy`) came from a free-text input the Angular UI let anyone type into — meaning the audit trail was trivially spoofable, not because anyone attempted it, but because there was no real identity to spoof it against yet.

FR-8's brief also names "object-ownership checks" specifically, not just role checks. D2's workflow gives that a concrete, non-speculative meaning here: an adjudication run has a natural owner (whoever started it), and an Adjuster is a genuinely different actor from that owner by design — they review and act on cases they did not themselves start. That asymmetry is the actual object-ownership check this ADR implements, not an invented scenario to satisfy the requirement.

## Decision

**Two roles** (`DomainCopilot.Domain.Identity.UserRole`): `Adjuster` (mandated by ADR-0008) and `Analyst` (this project's own choice for the second required role — can ingest, ask, and start adjudication runs, but never finalize one).

**No self-service registration.** Letting anyone register as Adjuster would defeat the entire point of a server-enforced role, and the brief doesn't require open registration — only that the roles be genuinely different and server-enforced. `DemoUserSeeder` (an `IHostedService`) seeds exactly one account per role on first startup against an empty `Users` table, from `SEED_ADJUSTER_PASSWORD`/`SEED_ANALYST_PASSWORD` env vars; leaving either blank skips seeding rather than creating an account with a known-empty password. A real deployment would replace this with an admin-driven provisioning flow — documented as a deliberate cut, not a silent gap.

**Password hashing**: PBKDF2-HMAC-SHA256 via the BCL's own `Rfc2898DeriveBytes` (210,000 iterations, OWASP's 2023+ minimum recommendation, 128-bit salt, constant-time comparison via `CryptographicOperations.FixedTimeEquals`), not a third-party crypto library — there is nothing new here to track against a dependency advisory database, which matters under this project's own "check the current advisory database, not just the version number" rule (CLAUDE.md).

**Tokens**: JWT (`System.IdentityModel.Tokens.Jwt` 8.22.0, well past the versions affected by CVE-2024-21319 — confirmed via `dotnet list package --vulnerable`), HMAC-SHA256-signed, carrying the username (`ClaimTypes.Name`) and role (`ClaimTypes.Role`) as claims so ASP.NET Core's own `[Authorize(Roles = ...)]` reads the role directly with no custom claims-transformation step. Every controller requires a valid bearer token by default (`AddAuthorization(options => options.FallbackPolicy = options.DefaultPolicy)` in `Program.cs`) — `AuthController.Login` is the one `[AllowAnonymous]` exception, alongside the health-check and (dev-only) OpenAPI endpoints, which are explicitly opted out rather than the default being open.

**Object-level authorization**: `AdjudicationCase.CreatedByUsername` (set at `Create`, from the authenticated caller's own username, never a client-supplied value) is the ownership record `AdjudicationController` checks — an Analyst may only `GET`/stream/download-memo a case they themselves started (`CanAccess`, `Forbid()` otherwise) and `ListRuns` returns only their own cases (`IAdjudicationCaseRepository.ListByCreatedByAsync`); an Adjuster bypasses this entirely (`IsAdjuster`), since D2's approval gate requires them to act on any case, not only ones they started. The finalize actions (`Approve`/`Reject`/`EditAndApprove`) are instead a pure role check (`[Authorize(Roles = nameof(UserRole.Adjuster))]`) — deliberately not an ownership check, for the same reason.

**A real fix this made necessary, not just an addition**: `AdjudicationCase.ApprovedBy` (the audit-trail field recording who approved/rejected/edited a decision) previously came from `ApprovalRequest.Actor`, a plain string the client supplied with no verification at all — the Angular UI even shipped a free-text "Acting as" input defaulting to a hardcoded email. `AdjudicationController.FinalizeAsync` now always uses the authenticated caller's own username (`CurrentUsername`, from the JWT's claims) instead, and the client-supplied `Actor` field was removed from `ApprovalRequest`/`EditAndApproveRequest` entirely rather than left as dead, ignorable input.

**A second real fix**: `AdjudicationService.streamRun` (the live per-agent-progress SSE view) used the browser's native `EventSource`, which cannot attach an `Authorization` header — meaning it would have silently 401'd the moment every endpoint started requiring a bearer token. It was rewritten to read the same SSE response via `fetch` (the same approach `RetrievalService.askStream` already used, for the unrelated reason that `EventSource` doesn't support `POST`), attaching the token manually and getting real cancellation (an aborted fetch) as a side effect, same as `askStream` already has.

## Alternatives considered

- **Full ASP.NET Core Identity** (`UserManager`/`RoleManager`/`IdentityDbContext`) — rejected as more machinery than this project's two fixed roles and no-self-registration model need; a custom `User` entity plus a thin password-hasher/token-service pair is smaller, easier to reason about end-to-end, and equally secure for this scope.
- **Cookie-based auth** — rejected because the Angular SPA and API are already split across origins in dev (CORS is already configured for `localhost:4200` → `localhost:5080`), and a stateless bearer token avoids CSRF/cookie-domain concerns entirely for that split.
- **Open self-service registration with a role picker** — rejected outright: letting a caller choose their own role at signup would make the "server-enforced" role distinction meaningless.

## Consequences

Every existing endpoint now requires a bearer token, which is a genuine breaking change to every prior manual/curl-based verification workflow used earlier in this project (documented here rather than silently absorbed) — anyone exercising the API by hand now needs to log in first. The Angular UI gates its approval actions behind `authService.isAdjuster()` for UX (hiding buttons a non-Adjuster couldn't use anyway), but every one of those checks is backed by the identical server-side check — the UI hiding it is not the actual control, and was verified as such live (a non-Adjuster token gets a real 403 from `/approve`, not just a hidden button).

What this doesn't cover, honestly: token refresh/revocation (a token is valid for its full `JWT_EXPIRY_MINUTES` window with no server-side revocation list — acceptable for this project's scope, not for a real multi-tenant deployment), and there is still only one account per role rather than a real many-analysts/many-adjusters model — sufficient to demonstrate genuinely different, server-enforced permissions, not a claim of production-readiness.
