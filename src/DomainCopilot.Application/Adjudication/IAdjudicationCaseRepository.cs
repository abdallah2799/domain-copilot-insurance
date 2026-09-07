using DomainCopilot.Domain.Adjudication;

namespace DomainCopilot.Application.Adjudication;

/// <summary>Port over the relational store for adjudication runs. Infrastructure provides the EF
/// Core/MSSQL implementation; Application never references EF Core directly.</summary>
public interface IAdjudicationCaseRepository
{
    Task<AdjudicationCase?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the case fresh from the database, bypassing any change tracking.
    ///
    /// Required by the progress stream, which re-reads the same row in a loop inside one request
    /// scope while a different scope (the background pipeline) writes to it. A tracked read returns
    /// the instance already in the identity map, so every poll after the first compares the initial
    /// snapshot against itself and reports no change -- the stream stayed silent for the whole run
    /// and the page only updated when navigating away and back created a new scope.
    /// </summary>
    Task<AdjudicationCase?> FindByIdForReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdjudicationCase>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>FR-8's object-ownership check: the cases an Analyst is actually allowed to see —
    /// only the ones they themselves started (see <see cref="AdjudicationCase.CreatedByUsername"/>).
    /// An Adjuster instead uses <see cref="ListAllAsync"/>, since D2's approval gate requires an
    /// Adjuster be able to review and act on any case, not only their own.</summary>
    Task<IReadOnlyList<AdjudicationCase>> ListByCreatedByAsync(string createdByUsername, CancellationToken cancellationToken = default);

    Task AddAsync(AdjudicationCase adjudicationCase, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
