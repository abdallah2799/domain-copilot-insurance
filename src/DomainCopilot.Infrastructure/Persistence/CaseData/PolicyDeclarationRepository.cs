using DomainCopilot.Application.CaseData;
using DomainCopilot.Domain.CaseData;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence.CaseData;

public sealed class PolicyDeclarationRepository(DomainCopilotDbContext dbContext) : IPolicyDeclarationRepository
{
    public Task<PolicyDeclaration?> FindByPolicyNumberAsync(string policyNumber, CancellationToken cancellationToken = default) =>
        dbContext.PolicyDeclarations.SingleOrDefaultAsync(p => p.PolicyNumber == policyNumber, cancellationToken);

    public async Task<IReadOnlyList<PolicyDeclaration>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.PolicyDeclarations.OrderBy(p => p.PolicyNumber).ToListAsync(cancellationToken);

    public Task AddAsync(PolicyDeclaration declaration, CancellationToken cancellationToken = default)
    {
        dbContext.PolicyDeclarations.Add(declaration);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
