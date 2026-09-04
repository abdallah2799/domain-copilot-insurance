using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Application.CaseData;

/// <summary>Port over the relational store for claim history facts. Infrastructure provides
/// the EF Core/MSSQL implementation; Application never references EF Core directly.</summary>
public interface IClaimHistoryRepository
{
    Task<ClaimHistoryRecord?> FindByClaimNumberAsync(string claimNumber, CancellationToken cancellationToken = default);

    /// <summary>Claims on the same policy with a date of loss within <paramref name="windowDays"/>
    /// days of <paramref name="referenceDate"/> on either side — a window centered on the reference
    /// date, not a historical lookback, matching the corpus's "within a 90-day window" phrasing
    /// (Claims Adjudication Guidelines, Section 3) rather than "in the preceding 90 days."</summary>
    Task<IReadOnlyList<ClaimHistoryRecord>> FindByPolicyNumberWithinWindowAsync(
        string policyNumber, DateOnly referenceDate, int windowDays, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimHistoryRecord>> ListAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ClaimHistoryRecord record, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
