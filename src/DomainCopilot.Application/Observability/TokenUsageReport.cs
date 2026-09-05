namespace DomainCopilot.Application.Observability;

public sealed record TokenUsageSummary(
    DateTimeOffset TimestampUtc,
    string CorrelationId,
    string AgentName,
    string ProviderName,
    string ModelName,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd);

/// <summary>FR-9's "per-request token/cost accounting... queryable via an API endpoint" -- the
/// totals are computed over every recorded row, not just <see cref="RecentEntries"/>, so the totals
/// stay accurate even once there are more rows than the recent-entries page shows.</summary>
public sealed record TokenUsageReport(
    IReadOnlyList<TokenUsageSummary> RecentEntries,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    decimal TotalEstimatedCostUsd);
