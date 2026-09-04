using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// Resolves which Policy Form version governs a given date of loss (ADR-0005) — the concrete fix
/// for D2's named risk of retrieval confidently answering from the wrong policy version. A
/// deterministic, pure lookup, not an LLM judgment call: the governing version is whichever
/// PolicyForm has the latest <see cref="Document.EffectiveDate"/> on or before the date of loss.
/// </summary>
public static class PolicyVersionResolver
{
    /// <summary>Returns null when no PolicyForm's effective date is on or before
    /// <paramref name="dateOfLoss"/> — e.g. a loss dated before this corpus's earliest policy
    /// edition — rather than guessing at the earliest or latest version available.</summary>
    public static string? Resolve(IReadOnlyList<Document> policyForms, DateOnly dateOfLoss) =>
        policyForms
            .Where(d => d.Category == DocumentCategory.PolicyForm && d.FormVersion is not null && d.EffectiveDate is not null)
            .Where(d => d.EffectiveDate!.Value <= dateOfLoss)
            .OrderByDescending(d => d.EffectiveDate!.Value)
            .Select(d => d.FormVersion)
            .FirstOrDefault();
}
