using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Application.CaseData;

/// <summary>Port over the relational store for policy Declarations facts. Infrastructure provides
/// the EF Core/MSSQL implementation; Application never references EF Core directly.</summary>
public interface IPolicyDeclarationRepository
{
    Task<PolicyDeclaration?> FindByPolicyNumberAsync(string policyNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyDeclaration>> ListAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(PolicyDeclaration declaration, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
