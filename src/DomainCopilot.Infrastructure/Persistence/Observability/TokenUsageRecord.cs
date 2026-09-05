namespace DomainCopilot.Infrastructure.Persistence.Observability;

/// <summary>A persisted row per completion call (FR-9) -- a persistence-only shape, not a Domain
/// aggregate with invariants of its own (the same reasoning as <c>ChunkRecord</c>), so it lives in
/// Infrastructure directly rather than Domain.</summary>
public sealed class TokenUsageRecord
{
    public Guid Id { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}
