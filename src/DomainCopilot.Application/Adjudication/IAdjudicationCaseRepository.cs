using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Port over the relational store for adjudication runs. Infrastructure provides the EF
/// Core/MSSQL implementation; Application never references EF Core directly.</summary>
public interface IAdjudicationCaseRepository
{
    Task<AdjudicationCase?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjudicationCase>> ListAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(AdjudicationCase adjudicationCase, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
