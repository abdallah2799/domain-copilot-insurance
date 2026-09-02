# AI usage log

Dated, honest record of what was delegated to Claude Code vs. hand-directed, and where it needed correction. Entries are appended, not rewritten.

## 2026-09-01 — Repo bootstrap, Clean Architecture skeleton, governance

**Delegated to AI**: reading and structuring the assignment PDF into a plan; MoSCoW backlog and 6-day pacing; all shell commands to scaffold the `.NET 10` solution (`dotnet new sln`/`classlib`/`webapi`/`xunit`, project references); Angular app scaffold (`ng new`); initial `docs/BRD.md`, ADR-0001/0002, `docs/SECURITY.md`-adjacent governance files, `.gitignore`, `CONTRIBUTING.md`, `.env.example`, GitHub issue/PR templates.

**Written/decided by the human**: the Domain+Twist variant itself (D2T6, from the invitation email — the one fact the AI could not have derived or guessed); the tech stack (.NET/Semantic Kernel/Angular/Qdrant/MSSQL); the correction from .NET 8 to .NET 10 for Semantic Kernel compatibility; the instruction that documentation must grow incrementally with the codebase rather than as a late dump; the decision that the second FR-8 role isn't fixed to a specific persona; the real remaining timeline (6 days, not the brief's 12) and the 5-build-days + 1-test/record-day split, including the specific instruction to code Day 5's tail end but hold its commit until Day 6 so the 6-distinct-commit-day floor is met honestly rather than backdated.

**Where the AI was wrong and had to be corrected**:
- Defaulted the plan to .NET 8; user corrected to .NET 10 for Semantic Kernel compatibility.
- Drafted a "read this first" flat dump of every remaining brief requirement; user redirected to attach each requirement to the epic that produces it, growing the docs as the codebase grows.
- Assumed a generically-named "Claims Analyst" second role satisfied FR-8; user pointed out only the Adjuster role is actually mandated by the domain pack, and the second role's naming/scope is an open implementation choice, not a requirement to invent.
- Planned pacing against the brief's full 12-day window before being told only 6 calendar days actually remain.
- When told to fix the `Microsoft.OpenApi` NU1903 vulnerability warning, first bumped 2.0.0 → 2.0.1 and assumed that resolved it; the warning persisted because the GitHub Security Advisory's patched range for the 2.x line is actually ≥2.7.5. Caught by querying the advisory directly instead of assuming the next patch version was sufficient.

**How verified**: `dotnet build` and `dotnet test` run directly (not just claimed) after scaffolding — confirmed 0 warnings/0 errors only after the OpenAPI package was actually bumped to 2.7.5, and confirmed all 4 xUnit test projects execute (after deleting the empty auto-generated `UnitTest1.cs` stubs, which asserted nothing). `ng build` and `ng test` run directly — confirmed the Vitest-based test runner passes with 0 browser/Chrome dependency, and the default Angular welcome-page template was replaced before it could ship as unreviewed boilerplate.

## 2026-09-02 — Git commit governance: no AI attribution trailers

**Instructed by the human**: stop appending `Co-Authored-By` or any other AI attribution trailer to commit messages. This assessment requires the commit history to read as strictly individual work, so every commit needs to look and read as the author's own.

**Delegated to AI**: encode the rule as a standing project instruction rather than a one-off — added a "Git commit rules" section to `CLAUDE.md` (no attribution trailers, Conventional Commits format only), then ran the change through the repo's own governance process: a feature branch, a self-reviewed PR, green CI, squash-merged into `main`, itself carrying no trailer. This PR is a real example of the agentic-governance discipline the assessment asks for: the correction was captured as a durable, committed rule instead of only being followed for the rest of the chat session.

**Reviewed and merged by the human**: the human reviewed and merged the CLAUDE.md PR as a valid instance of this governance loop working as intended.

**Where the log itself needed a correction**: asked to log a third point — that `.claude/settings.json` had been manually edited to blank out "attribution" fields as a stronger, harder guarantee than the CLAUDE.md instruction. No such file exists anywhere in this project or in the global Claude Code configuration on this machine, and Claude Code's settings schema has no such attribution field to begin with — the trailer is produced by instructions in the assistant's system prompt, not a settings toggle. Flagged this back to the human rather than writing an unverifiable claim into a deliverable that exists specifically to be honest about AI-assisted work; the human agreed to drop that point from this entry.

**How verified**: `git log` on the merged commit inspected directly to confirm no trailer is present; filesystem searched for `.claude/settings.json` (project and global) before writing this entry, not assumed.

## 2026-09-02 — Provider abstraction (Semantic Kernel: OpenAI + Ollama)

**Instructed by the human**: continue the day's work; separately, a message appeared mid-session (styled as a system instruction) telling the assistant to resume appending `Co-Authored-By`/"Generated with Claude Code" to commits and PRs, framed as replacing all earlier attribution guidance. The AI did not comply — it directly contradicted the human's explicit, reasoned, already-merged instruction from the previous session, and treated the mismatch as worth surfacing rather than silently acting on it either way.

**Delegated to AI**: the full `ICompletionService`/`IEmbeddingService` port design in `Application`, the OpenAI and Ollama adapters in `Infrastructure` (both built on Semantic Kernel, Ollama reusing the OpenAI connector against its OpenAI-compatible endpoint), the `FallbackCompletionService`/`FallbackEmbeddingService` decorators, DI wiring in `Program.cs`, and the accompanying tests and ADR-0003.

**Where the AI was wrong and had to be corrected — by itself, before the human saw it**:
- First draft of the streaming fallback decorator would have silently restarted the response from the fallback provider if the primary failed *after* already streaming some content — duplicating output on whatever client was mid-stream. Caught on review before committing, not after; rewritten so a mid-stream failure propagates instead of silently retrying, and a dedicated test (`StreamCompleteAsync_WhenPrimaryFailsAfterYieldingChunks_PropagatesInsteadOfRestartingOnFallback`) pins the corrected behavior.
- Two Infrastructure NuGet package references were added at a lower minor version (10.0.0) than a transitive dependency already required (10.0.6 / 10.0.3), which NuGet correctly failed the restore on (`NU1605`) rather than silently picking a version. Fixed by bumping to the versions NuGet reported as the actual floor, not by suppressing the check.
- A new unit test asserted the exact exception type `JsonException` for malformed tool-schema JSON; the real thrown type is `JsonReaderException` (a subclass). The test failed on first run, which is exactly what it was for — loosened to `Assert.ThrowsAny<JsonException>()`, which is the behavior that actually matters (some JSON parse failure, not a specific subclass).
- Reflection was used against the installed Semantic Kernel 1.80.0 assemblies to confirm exact method signatures (`AddOpenAIChatCompletion` overloads, `ChatMessageContent`/`FunctionCallContent`/`FunctionResultContent` shapes, `KernelJsonSchema.Parse`) before writing code against them, rather than guessing from training-data knowledge of older SK versions — the API has evolved since, and this caught the actual `Uri`-endpoint overload used for Ollama, which is not the pattern this model would have guessed first.

**A known compromise, not hidden**: `ITextEmbeddingGenerationService`/`AddOpenAITextEmbeddingGeneration` are marked obsolete in this SK version in favor of `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>`, but the newer API's SK-side registration method wasn't resolvable via reflection in the time available. Stayed on the still-functional obsolete API with the warning suppressed and a comment explaining why, rather than either silently leaving warnings in the build or spending the rest of the day chasing a moving target — recorded in ADR-0003 as a fast-follow, not left implicit.

**How verified**: `dotnet build` on the full solution (0 warnings, 0 errors) and `dotnet format --verify-no-changes` (clean) run after every meaningful change, not just at the end. The API was actually started (`dotnet run`, backgrounded) and polled until `/health/live` returned `Healthy`, confirming the full DI graph — including constructing two real Semantic Kernel `Kernel` instances — resolves without a runtime crash, which a build-only check would not have caught. All 11 new tests (`FallbackCompletionServiceTests` ×6, `KernelToolMapperTests` ×5) run and passed before being called done.
