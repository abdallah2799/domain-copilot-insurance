namespace DomainCopilot.Domain.Identity;

/// <summary>FR-8's two server-enforced roles. <see cref="Adjuster"/> is mandated by D2's approval
/// gate (ADR-0008) — only an Adjuster may approve/reject/edit-and-approve a run. <see
/// cref="Analyst"/> is this project's own choice for the second required role: can ingest, ask, and
/// start adjudication runs, but never finalize one.</summary>
public enum UserRole
{
    Analyst,
    Adjuster,
}
