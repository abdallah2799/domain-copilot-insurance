using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Retrieval;

/// <summary>A grounded-question request (FR-2/FR-6's "ask+citations"). Mirrors <see
/// cref="RetrievalQuery"/>'s version-pinning fields exactly, since asking is retrieval plus one
/// synthesis step on top, not a different query shape.</summary>
public sealed record AskRequest(
    string Question,
    int TopK = 5,
    DateOnly? DateOfLoss = null,
    string? FormVersion = null,
    DocumentCategory? Category = null);
