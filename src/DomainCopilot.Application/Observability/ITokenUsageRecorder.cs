namespace DomainCopilot.Application.Observability;

/// <summary>Port over FR-9's persisted per-request token/cost accounting. Infrastructure provides
/// the EF Core/MSSQL implementation and the actual cost estimate (a config-driven price table,
/// Application never computes a dollar figure itself).</summary>
public interface ITokenUsageRecorder
{
    Task RecordAsync(TokenUsageEntry entry, CancellationToken cancellationToken = default);
}
