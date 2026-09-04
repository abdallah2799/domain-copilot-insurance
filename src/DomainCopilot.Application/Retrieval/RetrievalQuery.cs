using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Retrieval;

/// <summary>
/// A retrieval request. <see cref="DateOfLoss"/> and <see cref="FormVersion"/> are mutually
/// exclusive ways to pin the query to a policy version — supplying a date lets
/// <see cref="PolicyVersionResolver"/> resolve it (the D2 scenario: "what does the policy say about
/// X, for a loss on this date"); an explicit <see cref="FormVersion"/> is for a caller that already
/// knows which edition it means. Both null means no version pin — reference material and every
/// version-tagged chunk are all in scope.
/// </summary>
public sealed record RetrievalQuery(
    string QueryText,
    int TopK = 5,
    DateOnly? DateOfLoss = null,
    string? FormVersion = null,
    DocumentCategory? Category = null);
