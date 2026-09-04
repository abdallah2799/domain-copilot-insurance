using DomainCopilot.Application.CaseData;
using DomainCopilot.Domain.CaseData;

namespace DomainCopilot.Application.Tests.CaseData;

internal sealed class FakePolicyDeclarationRepository : IPolicyDeclarationRepository
{
    private readonly Dictionary<string, PolicyDeclaration> _byPolicyNumber = new();
    private readonly List<PolicyDeclaration> _pending = [];

    public Task<PolicyDeclaration?> FindByPolicyNumberAsync(string policyNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byPolicyNumber.GetValueOrDefault(policyNumber));

    public Task<IReadOnlyList<PolicyDeclaration>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PolicyDeclaration>>([.. _byPolicyNumber.Values]);

    public Task AddAsync(PolicyDeclaration declaration, CancellationToken cancellationToken = default)
    {
        _pending.Add(declaration);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var declaration in _pending)
        {
            _byPolicyNumber[declaration.PolicyNumber] = declaration;
        }

        _pending.Clear();
        return Task.CompletedTask;
    }
}
